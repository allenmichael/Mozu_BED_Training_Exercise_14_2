using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mozu.Api;
using Autofac;
using Mozu.Api.ToolKit.Config;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace Mozu_BED_Training_Exercise_14_2
{
    [TestClass]
    public class MozuDataConnectorTests
    {
        private IApiContext _apiContext;
        private IContainer _container;

        [TestInitialize]
        public void Init()
        {
            _container = new Bootstrapper().Bootstrap().Container;
            var appSetting = _container.Resolve<IAppSetting>();
            var tenantId = int.Parse(appSetting.Settings["TenantId"].ToString());
            var siteId = int.Parse(appSetting.Settings["SiteId"].ToString());

            _apiContext = new ApiContext(tenantId, siteId);
        }

        [TestMethod]
        public void Exercise_14_1_Get_Orders()
        {
            //Create an Order resource. This resource is used to get, create, update orders
            var orderResource = new Mozu.Api.Resources.Commerce.OrderResource(_apiContext);

            //Filter orders by statuses
            var acceptedOrders = orderResource.GetOrdersAsync(filter: "Status eq 'Accepted'").Result;
            var closedOrders = orderResource.GetOrdersAsync(filter: "Status eq 'Closed'").Result;

            //Filter orders by acct number
            var orderByCustId = orderResource.GetOrdersAsync(filter: "CustomerAccountId eq '1001'").Result;

            //Filter orders by email
            var orderByEmail = orderResource.GetOrdersAsync(filter: "Email eq 'test@customer.com'").Result;

            //Filter orders by order number
            var existingOrders = orderResource.GetOrdersAsync(filter: "OrderNumber eq '1'").Result;

            //Initialize the Order variable
            Mozu.Api.Contracts.CommerceRuntime.Orders.Order existingOrder = null;
            //Check if an Order was returned
            if (existingOrders.TotalCount > 0)
            {
                //Set the Order to the first occurance in the collection
                existingOrder = existingOrders.Items[0];
            }

            if (existingOrder != null)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Order Status Values: " 
                    + Environment.NewLine +
                    "Status={0}" 
                    + Environment.NewLine + 
                    "FulfillmentStatus={1}" 
                    + Environment.NewLine + 
                    "PaymentStatus={2}" 
                    + Environment.NewLine + 
                    "ReturnStatus={3}",
                   existingOrder.Status,
                   existingOrder.FulfillmentStatus,
                   existingOrder.PaymentStatus,
                   existingOrder.ReturnStatus));


                //Write out payment statuses
                foreach (var existingPayment in existingOrder.Payments)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Payment Status Value[{0}]: Status={1}",
                        existingPayment.Id,
                        existingPayment.Status));

                    //Write out payment interaction statuses
                    foreach (var existingInteraction in existingPayment.Interactions)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("Payment Interaction Status Value[{0}]: Status={1}",
                            existingInteraction.Id,
                            existingInteraction.Status));
                    }
                }

                //Write out order package statuses
                foreach (var existingPackage in existingOrder.Packages)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Package Status Value[{0}]: Status={1}",
                        existingPackage.Id,
                        existingPackage.Status));
                }
            }
        }

        [TestMethod]
        public void Exercise_14_2_Auth_Capture_Order_Payment()
        {
            var orderNumber = 12;

            //Create an Order resource. This resource is used to get, create, update orders
            var orderResource = new Mozu.Api.Resources.Commerce.OrderResource(_apiContext);
            var paymentResource = new Mozu.Api.Resources.Commerce.Orders.PaymentResource(_apiContext);

            var existingOrder = (orderResource.GetOrdersAsync(filter: "OrderNumber eq '" + orderNumber + "'").Result).Items[0];
            Mozu.Api.Contracts.CommerceRuntime.Payments.Payment authorizedPayment = null;
            Mozu.Api.Contracts.CommerceRuntime.Payments.Payment pendingPayment = null;

            #region Add BillingInfo from Customer Object
            var customerResource = new Mozu.Api.Resources.Commerce.Customer.CustomerAccountResource(_apiContext);
            var customerAccount = customerResource.GetAccountAsync(1002).Result;

            var contactInfo = new Mozu.Api.Contracts.Core.Contact();

            foreach (var contact in customerAccount.Contacts)
            {
                foreach (var type in contact.Types)
                {
                    if (type.IsPrimary)
                    {
                        contactInfo.Address = contact.Address;
                        contactInfo.CompanyOrOrganization = contact.CompanyOrOrganization;
                        contactInfo.Email = contact.Email;
                        contactInfo.FirstName = contact.FirstName;
                        contactInfo.LastNameOrSurname = contact.LastNameOrSurname;
                        contactInfo.MiddleNameOrInitial = contact.MiddleNameOrInitial;
                        contactInfo.PhoneNumbers = contact.PhoneNumbers;
                    }
                }
            }

            var billingInfo = new Mozu.Api.Contracts.CommerceRuntime.Payments.BillingInfo()
            {
                BillingContact = contactInfo,
                IsSameBillingShippingAddress = true,
                PaymentType = "Check",
            };
            #endregion

            var action = new Mozu.Api.Contracts.CommerceRuntime.Payments.PaymentAction()
            {
                    Amount = existingOrder.Total,
                    CurrencyCode = "USD",
                    InteractionDate = DateTime.Now,
                    NewBillingInfo = billingInfo,
                    ActionName = "CreatePayment",
                    ReferenceSourcePaymentId = null,
                    CheckNumber = "1234"
            };
           
            try
            {
                authorizedPayment = existingOrder.Payments.FirstOrDefault(d => d.Status == "Authorized");
                pendingPayment = existingOrder.Payments.FirstOrDefault(d => d.Status == "Pending");
            }
            catch(Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

            if(authorizedPayment != null)
            {
                action.ActionName = "CapturePayment";
                var capturedPayment = paymentResource.PerformPaymentActionAsync(action, existingOrder.Id, authorizedPayment.Id).Result;
            }
            else if(pendingPayment != null)
            {
                action.ActionName = "CapturePayment";
                var capturedPayment = paymentResource.PerformPaymentActionAsync(action, existingOrder.Id, pendingPayment.Id).Result;
            }
            else
            {
                var authPayment = paymentResource.CreatePaymentActionAsync(action, existingOrder.Id).Result;
                
            }

            

        }
    }
}
