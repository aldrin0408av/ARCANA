﻿using Microsoft.AspNetCore.Mvc;
using RDF.Arcana.API.Common;
using RDF.Arcana.API.Data;
using RDF.Arcana.API.Features.Sales_Management.Sales_transactions;

namespace RDF.Arcana.API.Features.Sales_Management.Sales_Transactions;
[Route("api/sales-transaction"), ApiController]

public class VoidTransaction : ControllerBase
{
    private readonly IMediator _mediator;

    public VoidTransaction(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPut("{id}/void")]
    public async Task<IActionResult> Void([FromBody] VoidtransactionCommand command, [FromRoute] int id)
    {
        try
        {
            command.TransactionId = id;
            var result = await _mediator.Send(command);

            return result.IsFailure ? BadRequest(result) : Ok(result);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public class VoidtransactionCommand : IRequest<Result>
    {
        public int TransactionId { get; set; }
        public string Reason { get; set; }
    }

    public class Handler : IRequestHandler<VoidtransactionCommand, Result>
    {
        private readonly ArcanaDbContext _context;

        public Handler(ArcanaDbContext context)
        {
            _context = context;
        }

        public async Task<Result> Handle(VoidtransactionCommand request, CancellationToken cancellationToken)
        {
            var existingTransaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.Id == request.TransactionId, cancellationToken);

            if (existingTransaction is null)
            {
                return SalesTransactionErrors.NotFound();
            }

            var paymentTransactions = await _context.PaymentTransactions
                .Where(pt => pt.TransactionId == request.TransactionId)
                .ToListAsync(cancellationToken);

            var advancePayments = await _context.AdvancePayments
                .Where(ap => 
                ap.ClientId == existingTransaction.ClientId && 
                ap.IsActive && ap.Status != Status.Voided)
                .ToListAsync(cancellationToken);

            var totalPayments = paymentTransactions.Sum(payment => payment.TotalAmountReceived);

            for (var i = 0; i < paymentTransactions.Count; i++)
            {
                var paymentTransaction = paymentTransactions[i];
                if (paymentTransaction.PaymentMethod == PaymentMethods.AdvancePayment)
                {
                    foreach (var advancePayment in advancePayments)
                    {
                        if(advancePayment.Origin == Origin.Manual)
                        {
                          advancePayment.RemainingBalance = advancePayment.AdvancePaymentAmount;
                        }
                        else
                        {
                            advancePayment.IsActive = false;
                        }
                    }
                }
                if(paymentTransaction.PaymentMethod != PaymentMethods.AdvancePayment)
                {
                    paymentTransaction.IsActive = false;
                }
            }
            
            existingTransaction.Status = Status.Voided;
            existingTransaction.Reason = request.Reason;

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}