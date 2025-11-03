using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Bikestores.Models;

namespace Bikestores.Controllers
{
    public class PopularProductsReportController : Controller
    {
        private BikeStoresEntities db = new BikeStoresEntities();

        public class PopularProduct
        {
            public string ProductName { get; set; }
            public string BrandName { get; set; }
            public string CategoryName { get; set; }
            public int TimesOrdered { get; set; }
        }

        public ActionResult Index(DateTime? from, DateTime? to, string brand, string category)
        {
            var startDate = from ?? DateTime.Now.AddMonths(-1);
            var endDate = to ?? DateTime.Now;

            var query = db.order_items
                .Include(x => x.product.brand)
                .Include(x => x.product.category)
                .Include(x => x.order)
                .Where(x => x.order.order_date >= startDate && x.order.order_date <= endDate);

           
            if (!string.IsNullOrEmpty(brand))
                query = query.Where(x => x.product.brand.brand_name == brand);

            if (!string.IsNullOrEmpty(category))
                query = query.Where(x => x.product.category.category_name == category);

            var reportData = query
                .GroupBy(x => new
                {
                    x.product.product_name,
                    BrandName = x.product.brand != null ? x.product.brand.brand_name : "—",
                    CategoryName = x.product.category != null ? x.product.category.category_name : "—",
                })
                .Select(g => new PopularProduct
                {
                    ProductName = g.Key.product_name,
                    BrandName = g.Key.BrandName,
                    CategoryName = g.Key.CategoryName,
                    TimesOrdered = g.Sum(x => x.quantity)
                })
                .OrderByDescending(p => p.TimesOrdered)
                .Take(50) 
                .ToList();

            
            ViewBag.FromDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.ToDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedBrand = brand;
            ViewBag.SelectedCategory = category;

            
            ViewBag.Brands = db.brands.OrderBy(b => b.brand_name).Select(b => b.brand_name).ToList();
            ViewBag.Categories = db.categories.OrderBy(c => c.category_name).Select(c => c.category_name).ToList();

            return View(reportData);
        }

        [HttpPost]
        public JsonResult SaveReport(string fileName, string base64Pdf)
        {
            try
            {
                var folderPath = Server.MapPath("~/Reports");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var fullPath = Path.Combine(folderPath, fileName + ".pdf");
                var bytes = Convert.FromBase64String(base64Pdf);
                System.IO.File.WriteAllBytes(fullPath, bytes);

                return Json(new { success = true, message = "Report saved successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving report: " + ex.Message });
            }
        }

        public ActionResult Archive(string from, string to, string brand, string category)
        {
            ViewBag.FromDate = from ?? "";
            ViewBag.ToDate = to ?? "";
            ViewBag.SelectedBrand = brand ?? "";
            ViewBag.SelectedCategory = category ?? "";

            var folderPath = Server.MapPath("~/Reports");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var files = new DirectoryInfo(folderPath)
                .GetFiles("*.pdf")
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            return PartialView("_Archive", files);
        }

        public FileResult Download(string filename)
        {
            var path = Path.Combine(Server.MapPath("~/Reports"), filename);
            if (!System.IO.File.Exists(path))
                throw new FileNotFoundException("File not found.");

            return File(path, "application/pdf", filename);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string filename, string from, string to, string brand, string category)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(filename))
                {
                    var path = Path.Combine(Server.MapPath("~/Reports"), filename);
                    if (System.IO.File.Exists(path))
                        System.IO.File.Delete(path);

                    TempData["SuccessMessage"] = $"Deleted '{filename}' successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Filename is invalid.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting report: {ex.Message}";
            }

            
            return RedirectToAction("Index", new { from, to, brand, category });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}
