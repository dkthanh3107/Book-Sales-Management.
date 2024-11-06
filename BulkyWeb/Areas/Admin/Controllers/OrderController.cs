using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;

namespace BulkyWeb.Areas.Admin.Controllers
{
    [Area("admin")]
    [Authorize]
    public class OrderController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        [BindProperty]
        public OrderVM OrderVM { get; set; }
        public OrderController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Details(int orderId)
        {
            OrderVM = new()
            {
                Order = _unitOfWork.Order.Get(u => u.Order_Id == orderId, includeProperties: "ApplicationUser"),
                OrderDetails = _unitOfWork.OrderDetail.GetAll(u => u.OrderId == orderId, includeProperties: "Product")
            };
            return View(OrderVM);
        }
        [HttpPost]
        [Authorize(Roles =SD.Role_Admin+","+SD.Role_Employee)]
        public IActionResult UpdateOrderDetails(int orderId)
        {
            var orderFromDb = _unitOfWork.Order.Get(u => u.Order_Id == OrderVM.Order.Order_Id);
            orderFromDb.Name = OrderVM.Order.Name;
            orderFromDb.PhoneNumber = OrderVM.Order.PhoneNumber;
            orderFromDb.StreetAddress = OrderVM.Order.StreetAddress;
            orderFromDb.City = OrderVM.Order.City;
            orderFromDb.State = OrderVM.Order.State;
            orderFromDb.PostalCode = OrderVM.Order.PostalCode;
            if(!string.IsNullOrEmpty(OrderVM.Order.Carrier))
            {
                orderFromDb.Carrier = OrderVM.Order.Carrier;
            }
            if (!string.IsNullOrEmpty(OrderVM.Order.TrackingNumber))
            {
                orderFromDb.TrackingNumber = OrderVM.Order.TrackingNumber;
            }
            _unitOfWork.Order.Update(orderFromDb);
            _unitOfWork.Save();

            TempData["Success"] = "Order Details Updated Successfully.";
            return RedirectToAction(nameof(Details), new {orderId = orderFromDb.Order_Id});
        }
        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult StartProcessing()
        {
            _unitOfWork.Order.UpdateStatus(OrderVM.Order.Order_Id, SD.StatusInProcess);
            _unitOfWork.Save();
            TempData["Success"] = "Order Details Updated Successfully.";
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.Order.Order_Id });
        }
        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult ShipOrder()
        {
            var order = _unitOfWork.Order.Get(u=>u.Order_Id == OrderVM.Order.Order_Id);
            order.TrackingNumber = OrderVM.Order.TrackingNumber;
            order.Carrier = OrderVM.Order.Carrier;
            order.OrderStatus = SD.StatusShipped;
            order.ShippingDate = DateTime.Now;
            if(order.PaymentStatus == SD.PaymentStatusDelayedPayment)
            {
                order.PaymemtDueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)).ToDateTime(TimeOnly.MinValue);
            }
            _unitOfWork.Order.Update(order);
            _unitOfWork.Save();
            TempData["Success"] = "Order Shipper Successfully.";
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.Order.Order_Id });
        }
        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult CancelOrder()
        {
            var order = _unitOfWork.Order.Get(u=>u.Order_Id==OrderVM.Order.Order_Id);

            if (order.PaymentStatus == SD.StatusApproved)
            {
                var options = new RefundCreateOptions
                {
                    Reason = RefundReasons.RequestedByCustomer,
                    PaymentIntent = order.PaymentIntentId,
                };
                var service = new RefundService();
                Refund refund = service.Create(options);

                _unitOfWork.Order.UpdateStatus(order.Order_Id, SD.StatusCancelled, SD.StatusRefunded);
            }
            else 
            {
                _unitOfWork.Order.UpdateStatus(order.Order_Id, SD.StatusCancelled, SD.StatusRefunded);
            }
            _unitOfWork.Save();
            TempData["Success"] = "Order Cancelled Successfully.";
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.Order.Order_Id });
        }
        [ActionName("Details")]
        [HttpPost]
        public IActionResult Details_PAY_NOW()
        {
            OrderVM.Order=_unitOfWork.Order.Get(u=>u.Order_Id == OrderVM.Order.Order_Id , includeProperties:"ApplicationUser");
            OrderVM.OrderDetails=_unitOfWork.OrderDetail.GetAll(u=>u.OrderId == OrderVM.Order.Order_Id , includeProperties:"Product");

            //it is a regular customer account and we need to capture payment
            //stripe logic
            var domain = Request.Scheme + "://" + Request.Host.Value + "/";
            var options = new SessionCreateOptions
            {
                SuccessUrl = domain + $"admin/order/PaymentConfirmation?orderId={OrderVM.Order.Order_Id}",
                CancelUrl = domain + $"admin/order/details?orderId={OrderVM.Order.Order_Id}",
                LineItems = new List<SessionLineItemOptions>(),
                Mode = "payment",
            };

            foreach (var item in OrderVM.OrderDetails)
            {
                var sessionLineItem = new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.Price * 100), // $20.50 => 2050
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product.Title
                        }
                    },
                    Quantity = item.Count
                };
                options.LineItems.Add(sessionLineItem);
            }


            var service = new SessionService();
            Session session = service.Create(options);
            _unitOfWork.Order.UpdateStripePaymentId(OrderVM.Order.Order_Id, session.Id, session.PaymentIntentId);
            _unitOfWork.Save();
            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);
        }

        public IActionResult PaymentConfirmation(int orderId)
        {
            Order order = _unitOfWork.Order.Get(u => u.Order_Id == orderId);
            if (order.PaymentStatus == SD.PaymentStatusDelayedPayment)
            {
                //this is an order by company
                var service = new SessionService();
                Session session = service.Get(order.SessionsId);
                if (session.PaymentStatus.ToLower() == "paid")
                {
                    _unitOfWork.Order.UpdateStripePaymentId(orderId, session.Id, session.PaymentIntentId);
                    _unitOfWork.Order.UpdateStatus(orderId,order.OrderStatus, SD.PaymentStatusApproved);
                    _unitOfWork.Save();
                }
            } 
            if (orderId <= 0)
            {
                return NotFound();
            }
            return View(orderId);
        }
        #region API CALLS
        [HttpGet]
        public IActionResult GetAll(string status)
        {
            IEnumerable<Order> orders;

            if(User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
            {
                orders = _unitOfWork.Order.GetAll(includeProperties: "ApplicationUser").ToList();
            }    
            else
            {
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
                orders = _unitOfWork.Order.GetAll(u => u.ApplicationUserId == userId, includeProperties:"ApplicationUser");
            }    
            switch(status)
            {
                case "pending":
                    orders = orders.Where(u => u.PaymentStatus == SD.PaymentStatusDelayedPayment);
                    break;
                case "inprocess":
                    orders = orders.Where(u => u.OrderStatus == SD.StatusInProcess);
                    break;
                case "approved":
                    orders = orders.Where(u => u.OrderStatus == SD.StatusApproved);
                    break;
                case "completed":
                    orders = orders.Where(u => u.OrderStatus == SD.StatusShipped);
                    break;
                default:
                    break;
            }
            return Json(new { data = orders });
        }
        #endregion
    }
}
