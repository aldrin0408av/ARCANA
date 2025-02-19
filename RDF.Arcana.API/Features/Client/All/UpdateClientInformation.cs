﻿using Microsoft.AspNetCore.Mvc;
using RDF.Arcana.API.Common;
using RDF.Arcana.API.Common.Helpers;
using RDF.Arcana.API.Data;
using RDF.Arcana.API.Domain;
using RDF.Arcana.API.Features.Client.Errors;
using RDF.Arcana.API.Features.Requests_Approval;
using RDF.Arcana.API.Features.Setup.Mode_Of_Payment;
using RDF.Arcana.API.Features.Setup.Store_Type;
using RDF.Arcana.API.Features.Setup.Term_Days;
using RDF.Arcana.API.Features.Setup.Terms;
using System.Security.Claims;
using static RDF.Arcana.API.Features.Listing_Fee.GetAllClientsInListingFee.GetAllClientsInListingFeeResult;

namespace RDF.Arcana.API.Features.Client.All;
[Route("api/Client"), ApiController]

public class UpdateClientInformation : ControllerBase
{
    private readonly IMediator _mediator;

    public UpdateClientInformation(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPut("UpdateClientInformation/{id:int}")]
    public async Task<IActionResult> UpdateClient(UpdateClientInformationCommand command, 
        [FromRoute] int id, 
        [FromQuery] int listingFeeRequestId, 
        [FromQuery] int otherExpensesRequestId)
    {
        try
        {
            if(User.Identity is ClaimsIdentity identity
                && IdentityHelper.TryGetUserId(identity, out var userId))
            {
                var roleClaim = identity.Claims.SingleOrDefault(c => c.Type == ClaimTypes.Role);

                if (roleClaim != null)
                {
                    command.RoleName = roleClaim.Value;
                }
            }

            command.ClientId = id;
            command.ListingFeeRequestId = listingFeeRequestId;
            command.ExpensesRequestId = otherExpensesRequestId;

            var result = await _mediator.Send(command);

            if (result.IsFailure)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    public sealed record UpdateClientInformationCommand : IRequest<Result>
    {
        public string RoleName { get; set; }

        public int ClientId { get; set; }
        public int? ListingFeeRequestId { get; set; }
        public int? ExpensesRequestId { get; set; }
        public int Expense { get; set; }
        public string OwnersName { get; set; }
        public OwnersAddressToUpdate OwnersAddress { get; set; }
        public string EmailAddress { get; set; }
        public string PhoneNumber { get; set; }
        public DateOnly DateOfBirth { get; set; }
        public string TinNumber { get; set; }
        public string BusinessName { get; set; }
        public int StoreTypeId { get; set; }
        public BusinessAddressToUpdate BusinessAddress { get; set; }
        public string AuthorizedRepresentative { get; set; }
        public string AuthorizedRepresentativePosition { get; set; }
        public int ClusterId { get; set; }
        public string Freezer { get; set; }
        public string TypeOfCustomer { get; set; }
        public bool DirectDelivery { get; set; }
        public int BookingCoverageId { get; set; }
        public ICollection<ModeOfPayment> ModeOfPayments { get; set; }
        public int TermsId { get; set; }
        public int? CreditLimit { get; set; }
        public int PriceModeId { get; set; }
        public int? TermDaysId { get; set; }
        public bool VariableDiscount { get; set; }
        public string Longitude { get; set; }
        public string Latitude { get; set; }
        public int UpdatedBy { get; set; }
        public FixedDiscountToUpdate FixedDiscount { get; set; }
        
        public class ModeOfPayment
        {
            public int ModeOfPaymentId { get; set; }
        }

        public class FixedDiscountToUpdate
        {
            public decimal? DiscountPercentage { get; set; }
        }
        public class BusinessAddressToUpdate
        {
            public string HouseNumber { get; set; }
            public string StreetName { get; set; }
            public string BarangayName { get; set; }
            public string City { get; set; }
            public string Province { get; set; }
        }

        public class OwnersAddressToUpdate
        {
            public string HouseNumber { get; set; }
            public string StreetName { get; set; }
            public string BarangayName { get; set; }
            public string City { get; set; }
            public string Province { get; set; }
        }
        
    }
    
    public class Handler : IRequestHandler<UpdateClientInformationCommand, Result>
    {
        private readonly ArcanaDbContext _context;

        public Handler(ArcanaDbContext context)
        {
            _context = context;
        }

        public async Task<Result> Handle(UpdateClientInformationCommand request, CancellationToken cancellationToken)
        {
            var existingClient = await _context.Clients
                .Include(fr => fr.Freezer)
                .Include(st => st.StoreType)
                .Include(fd => fd.FixedDiscounts)
                .Include(to => to.Term)
                .ThenInclude(tt => tt.Terms)
                .Include(to => to.Term)
                .ThenInclude(td => td.TermDays)
                .Include(ba => ba.BusinessAddress)
                .Include(oa => oa.OwnersAddress)
                .Include(bc => bc.BookingCoverages)
                .Include(fr => fr.FreebiesRequests)
                .Include(clients => clients.Request)
                .ThenInclude(approval => approval.Approvals)
                .Include(clients => clients.ClientModeOfPayment)
                .Include(lf => lf.ListingFees)
                .ThenInclude(rq => rq.Request)
                .ThenInclude(ap => ap.Approvals)
                .Include(ex => ex.Expenses)
                .ThenInclude(rq => rq.Request)
                .ThenInclude(ap => ap.Approvals)
                .FirstOrDefaultAsync(client => client.Id == request.ClientId, cancellationToken);

            
            //Validate if the Client Business is already registered
            var validateBusinessName = await _context.Clients.Where(
                client => client.Fullname == request.OwnersName &&
                     client.StoreType.Id == request.StoreTypeId &&
                     client.BusinessName == request.BusinessName &&
                     client.BusinessAddress.City == request.BusinessAddress.City &&
                     client.BusinessAddress.StreetName == request.BusinessAddress.BarangayName &&
                     client.RegistrationStatus != Status.Voided).FirstOrDefaultAsync(cancellationToken);
            
            //Validate if the store type is existing
            var validateStoreType = await _context.StoreTypes
                .FirstOrDefaultAsync(st => st.Id == request.StoreTypeId, cancellationToken);

            //Validate if the term is existing
            var validateTerm = await _context.Terms
                .FirstOrDefaultAsync(t => t.Id == request.TermsId, cancellationToken);

            //Validate if the term days is existing
            var validateTermDay = await _context.TermDays
                .FirstOrDefaultAsync(td => td.Id == request.TermDaysId, cancellationToken);
            
            //Get all the existing approver for the request
            var approver = await _context.RequestApprovers
                .Where(x => x.RequestId == existingClient.RequestId)
                .OrderBy(x => x.Level)
                .ToListAsync(cancellationToken);

            //Get all the existing approver for the listing fee request
            var listingFeeApprover = await _context.RequestApprovers
                .Where(x => x.RequestId == existingClient.RequestId)
                .OrderBy(x => x.Level)
                .ToListAsync(cancellationToken);

            //Get all the existing approver for the request
            var expensesApprover = await _context.RequestApprovers
                .Where(x => x.RequestId == existingClient.RequestId)
                .OrderBy(x => x.Level)
                .ToListAsync(cancellationToken);

            //Validate if the Freezer Asset Tag is already exisit
            var freezer = await _context.Freezers
                .FirstOrDefaultAsync(fr => 
                fr.AssetTag == request.Freezer,
                cancellationToken);

            if (request.Freezer is null)
            {
                existingClient.FreezerId = null;
            }
            else if (freezer == null && request.Freezer is not null)
            {
                var newFreezer = new Freezer
                {
                    AssetTag = request.Freezer
                };

                _context.Freezers.Add(newFreezer);

                existingClient.FreezerId = newFreezer.Id;
            }
            else
            {
               var alreadyUsedFreezer =  await _context.Clients
                    .FirstOrDefaultAsync(x => 
                    x.Freezer.AssetTag == request.Freezer && 
                    x.Id != request.ClientId, 
                    cancellationToken);

                if(alreadyUsedFreezer is not null)
                {
                    return ClientErrors.FreezerAlreadyTagged(request.Freezer);
                }

                existingClient.FreezerId = freezer.Id;
            }

            if (!approver.Any())
            {
                return ApprovalErrors.NoApproversFound(Modules.RegistrationApproval);
            }

            if (validateStoreType is null)
            {
                return StoreTypeErrors.NotFound();
            }

            if (validateTerm is null)
            {
                return TermErrors.NotFound();
            }

            if (validateTermDay is null && request.TermDaysId != null)
            {
                return TermDaysErrors.NotFound();
            }
            
            if (validateBusinessName != null)
            {
                return ClientErrors.AlreadyExist(validateBusinessName.Fullname);
            }
            
            if (existingClient is null)
            {
                return ClientErrors.NotFound();
            }
            
            var existingModeOfPayment = existingClient.ClientModeOfPayment.Select(x => x.ModeOfPaymentId).ToList();
            var modeOfPaymentToRemove = existingModeOfPayment.Except(request.ModeOfPayments.Select(c => c.ModeOfPaymentId)).ToList();

            foreach (var modeOfPayment in modeOfPaymentToRemove)
            {
                var forRemove = existingClient.ClientModeOfPayment.FirstOrDefault(x => x.ModeOfPaymentId == modeOfPayment);
                if (forRemove != null)
                {
                    existingClient.ClientModeOfPayment.Remove(forRemove);
                }
            }

            // Add and validate multiple mop to the client
            foreach (var modeOfPayment in request.ModeOfPayments)
            {
                var existingModePayment = await _context.ModeOfPayments.FirstOrDefaultAsync(x =>
                        x.Id == modeOfPayment.ModeOfPaymentId,
                    cancellationToken
                );

                if (existingModePayment == null)
                {
                    return ModeOfPaymentErrors.NotFound();
                }

                var clientModeOfPayment = await _context.ClientModeOfPayments.FirstOrDefaultAsync(
                    x => x.ClientId == request.ClientId && x.ModeOfPaymentId == existingModePayment.Id,
                    cancellationToken);

                if (clientModeOfPayment != null)
                {
                    //If existing update it
                    clientModeOfPayment.ModeOfPaymentId = existingModePayment.Id;
                    _context.ClientModeOfPayments.Update(clientModeOfPayment);
                }
                else
                {
                    // if there's no existing relation create it
                    var newPaymentMethod = new ClientModeOfPayment
                    {
                        ModeOfPaymentId = existingModePayment.Id,
                        ClientId = request.ClientId
                    };

                    _context.ClientModeOfPayments.Add(newPaymentMethod);
                }
            }


            existingClient.Fullname = request.OwnersName;
            existingClient.OwnersAddress.HouseNumber = request.OwnersAddress.HouseNumber;
            existingClient.OwnersAddress.StreetName = request.OwnersAddress.StreetName;
            existingClient.OwnersAddress.Province = request.OwnersAddress.Province;
            existingClient.OwnersAddress.Barangay = request.OwnersAddress.BarangayName;
            existingClient.OwnersAddress.City = request.OwnersAddress.City;
            existingClient.EmailAddress = request.EmailAddress;
            existingClient.PhoneNumber = request.PhoneNumber;
            existingClient.DateOfBirthDB = request.DateOfBirth;
            existingClient.TinNumber = request.TinNumber;
            existingClient.BusinessName = request.BusinessName;
            existingClient.StoreTypeId = request.StoreTypeId;
            existingClient.BusinessAddress.HouseNumber = request.BusinessAddress.HouseNumber;
            existingClient.BusinessAddress.StreetName = request.BusinessAddress.StreetName;
            existingClient.BusinessAddress.Province = request.BusinessAddress.Province;
            existingClient.BusinessAddress.City = request.BusinessAddress.City;
            existingClient.BusinessAddress.Barangay = request.BusinessAddress.BarangayName;
            existingClient.RepresentativeName = request.AuthorizedRepresentative;
            existingClient.RepresentativePosition = request.AuthorizedRepresentativePosition;
            existingClient.ClusterId = request.ClusterId;
            existingClient.PriceModeId = request.PriceModeId;
            existingClient.CustomerType = request.TypeOfCustomer;
            existingClient.DirectDelivery = request.DirectDelivery;
            existingClient.BookingCoverageId = request.BookingCoverageId;
            existingClient.Term.TermsId = request.TermsId;
            existingClient.Term.CreditLimit = request.CreditLimit;
            existingClient.Term.TermDaysId = request.TermDaysId;
            existingClient.VariableDiscount = request.VariableDiscount;
            existingClient.Longitude = request.Longitude;
            existingClient.Latitude = request.Latitude;
            

            if(request.RoleName == Roles.Cdo)
            {
                existingClient.Request.Status = Status.UnderReview;
                existingClient.RegistrationStatus = Status.UnderReview;
                existingClient.Request.CurrentApproverId = approver.First().ApproverId;

                foreach (var approval in existingClient.Request.Approvals)
                {
                    approval.IsActive = false;
                }

                var newUpdateHistory = new UpdateRequestTrail(
                    existingClient.RequestId,
                    Modules.RegistrationApproval,
                    DateTime.Now,
                    request.UpdatedBy);

                await _context.UpdateRequestTrails.AddAsync(newUpdateHistory, cancellationToken);

                var notificationForCurrentApprover = new Domain.Notification
                {
                    UserId = approver.First().ApproverId,
                    Status = Status.PendingClients
                };

                await _context.Notifications.AddAsync(notificationForCurrentApprover, cancellationToken);

                var notification = new Domain.Notification
                {
                    UserId = existingClient.AddedBy,
                    Status = Status.PendingClients
                };

                await _context.Notifications.AddAsync(notification, cancellationToken);


                //if(existingClient.RegistrationStatus == Status.Rejected)
                //{
                //    //Expenses

                //    var expenses = await _context.Requests
                //        .Include(x => x.Expenses)
                //        .FirstOrDefaultAsync(x => x.Id == request.ExpensesRequestId, cancellationToken);
                //    if (expenses is not null)
                //    {
                //        expenses.Status = Status.UnderReview;
                //        expenses.CurrentApproverId = listingFeeApprover.First().ApproverId;
                //        expenses.Expenses.Status = Status.UnderReview;

                //        foreach (var approval in expenses.Approvals)
                //        {
                //            approval.IsActive = false;
                //        }
                //        await _context.SaveChangesAsync(cancellationToken);

                //    }


                //    //Listing Fee

                //    var listingFee = await _context.Requests
                //        .Include(x => x.ListingFee)
                //        .FirstOrDefaultAsync(x => x.Id == request.ListingFeeRequestId, cancellationToken);
                //    if (listingFee is not null)
                //    {
                //        listingFee.Status = Status.UnderReview;
                //        listingFee.CurrentApproverId = listingFeeApprover.First().ApproverId;
                //        listingFee.ListingFee.Status = Status.UnderReview;

                //        foreach (var approval in listingFee.Approvals)
                //        {
                //            approval.IsActive = false;
                //        }
                //        await _context.SaveChangesAsync(cancellationToken);

                //    }
                //}

               

            }

            if (request.FixedDiscount.DiscountPercentage.HasValue)
            {
                if (existingClient.FixedDiscounts != null)
                {
                    existingClient.FixedDiscounts.DiscountPercentage = request.FixedDiscount.DiscountPercentage;
                }
                
                var fixedDiscount = new FixedDiscounts
                {
                    DiscountPercentage = request.FixedDiscount.DiscountPercentage / 100
                };

                await _context.FixedDiscounts.AddAsync(fixedDiscount, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                var discountId = fixedDiscount.Id;
                existingClient.FixedDiscountId = discountId;
                
            }
            else
            {
                existingClient.VariableDiscount = true;
                existingClient.FixedDiscountId = null;
            }
            
            
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}