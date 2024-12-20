using Bulky.DataAccess.Repository;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using System.Security.Claims;

namespace BulkyWeb.Areas.Customer.Controllers
{
    [Area("customer")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        [BindProperty]
        public ShoppingCartVM ShoppingCartVM { get; set; }

        public CartController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return NotFound();
            }

            ShoppingCartVM = new ShoppingCartVM
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product") ?? new List<ShoppingCart>(),
                Order = new Order()
            };

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                if (cart.Product != null)
                {
                    cart.Price = GetPriceBasedOnQuantity(cart);
                    ShoppingCartVM.Order.OrderTotal += (cart.Price * cart.Count);
                }
            }
            return View(ShoppingCartVM);
        }

        public IActionResult Summary()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return NotFound();
            }

            ShoppingCartVM = new ShoppingCartVM
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product") ?? new List<ShoppingCart>(),
                Order = new Order()
            };

            var applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);
            if (applicationUser != null)
            {
                ShoppingCartVM.Order.ApplicationUser = applicationUser;
                ShoppingCartVM.Order.Name = applicationUser.Name;
                ShoppingCartVM.Order.PhoneNumber = applicationUser.PhoneNumber;
                ShoppingCartVM.Order.StreetAddress = applicationUser.StressAddress;
                ShoppingCartVM.Order.City = applicationUser.City;
                ShoppingCartVM.Order.State = applicationUser.State;
                ShoppingCartVM.Order.PostalCode = applicationUser.PostalCode;
            }
            else
            {
                return NotFound();
            }

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                if (cart.Product != null)
                {
                    cart.Price = GetPriceBasedOnQuantity(cart);
                    ShoppingCartVM.Order.OrderTotal += (cart.Price * cart.Count);
                }
            }
            return View(ShoppingCartVM);
        }

        [HttpPost]
        [ActionName("Summary")]
        public IActionResult SummaryPOST(string paymentMethod)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product");

            ShoppingCartVM.Order.OrderDate = DateTime.Now;
            ShoppingCartVM.Order.ApplicationUserId = userId;

            ApplicationUser applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.Order.OrderTotal += (cart.Price * cart.Count);
            }
            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                ShoppingCartVM.Order.PaymentStatus = SD.PaymentStatusPending;
                ShoppingCartVM.Order.OrderStatus = SD.StatusPending;
            }
            else
            {
                ShoppingCartVM.Order.PaymentStatus = SD.PaymentStatusDelayedPayment;
                ShoppingCartVM.Order.OrderStatus = SD.StatusApproved;
            }

            _unitOfWork.Order.Add(ShoppingCartVM.Order);
            _unitOfWork.Save();
            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                OrderDetail orderDetail = new()
                {
                    ProductId = cart.ProductId,
                    OrderId = ShoppingCartVM.Order.Order_Id,
                    Price = cart.Price,
                    Count = cart.Count
                };
                _unitOfWork.OrderDetail.Add(orderDetail);
                _unitOfWork.Save();
            }
            if (paymentMethod == "direct")
            {
                ShoppingCartVM.Order.PaymentStatus = SD.PaymentStatusPaid;  // Thanh toán thành công
                ShoppingCartVM.Order.OrderStatus = SD.StatusInProcess;  // Đơn hàng đã hoàn tất
                _unitOfWork.Save();
                return RedirectToAction(nameof(OrderConfirmation), new { id = ShoppingCartVM.Order.Order_Id });
            }
            else if (paymentMethod == "stripe")
            {
                var domain = Request.Scheme + "://" + Request.Host.Value + "/";
                var options = new SessionCreateOptions
                {
                    SuccessUrl = domain + $"customer/cart/OrderConfirmation?id={ShoppingCartVM.Order.Order_Id}",
                    CancelUrl = domain + "customer/cart/index",
                    LineItems = new List<SessionLineItemOptions>(),
                    Mode = "payment",
                };

                // Thêm các sản phẩm vào Stripe Session
                foreach (var item in ShoppingCartVM.ShoppingCartList)
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
                _unitOfWork.Order.UpdateStripePaymentId(ShoppingCartVM.Order.Order_Id, session.Id, session.PaymentIntentId);
                _unitOfWork.Save();
                Response.Headers.Add("Location", session.Url);
                return new StatusCodeResult(303);
            }
            return BadRequest();
        }

        public IActionResult OrderConfirmation(int id)
        {
            Order order = _unitOfWork.Order.Get(u => u.Order_Id == id, includeProperties: "ApplicationUser");
            if (order == null || id <= 0)
            {
                return NotFound();
            }
            if (order.PaymentStatus == SD.PaymentStatusDelayedPayment)
            {
                var service = new SessionService();
                Session session = service.Get(order.SessionsId); 
                if (session.PaymentStatus.ToLower() == "paid")
                {                   
                    _unitOfWork.Order.UpdateStripePaymentId(id, session.Id, session.PaymentIntentId);
                    _unitOfWork.Order.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
                    _unitOfWork.Save();
                }
            }
            else if (order.PaymentStatus == SD.PaymentStatusPaid)
            {
                _unitOfWork.Order.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusPaid);
                _unitOfWork.Save();
            }
            List<ShoppingCart> shoppingCarts = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == order.ApplicationUserId).ToList();
            _unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
            _unitOfWork.Save();
            HttpContext.Session.Clear();

            return View(id);
        }


        public IActionResult Plus(int cartid)
        {
            var carFromDb = _unitOfWork.ShoppingCart.Get(u => u.ShoppingCart_Id == cartid);
            if (carFromDb != null)
            {
                carFromDb.Count += 1;
                _unitOfWork.ShoppingCart.Update(carFromDb);
                _unitOfWork.Save();
            }
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Minus(int cartid)
        {
            var carFromDb = _unitOfWork.ShoppingCart.Get(u => u.ShoppingCart_Id == cartid,tracked:true);
            if (carFromDb != null)
            {
                if (carFromDb.Count <= 1)
                {
                    HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == carFromDb.ApplicationUserId).Count() - 1);
                    _unitOfWork.ShoppingCart.Remove(carFromDb);
                }
                else
                {
                    carFromDb.Count -= 1;
                    _unitOfWork.ShoppingCart.Update(carFromDb);
                }
                _unitOfWork.Save();
            }
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Remove(int cartid)
        {
            var carFromDb = _unitOfWork.ShoppingCart.Get(u => u.ShoppingCart_Id == cartid,tracked:true);
            HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == carFromDb.ApplicationUserId).Count() - 1);
            _unitOfWork.ShoppingCart.Remove(carFromDb); 
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        private double GetPriceBasedOnQuantity(ShoppingCart shoppingCart)
        {
            if (shoppingCart.Count <= 50)
            {
                return shoppingCart.Product.Price;
            }
            else if (shoppingCart.Count <= 100)
            {
                return shoppingCart.Product.Price50;
            }
            else
            {
                return shoppingCart.Product.Price100;
            }
        }

        private string GetUserId()
        {
            var claimsIdentity = User.Identity as ClaimsIdentity;
            return claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
