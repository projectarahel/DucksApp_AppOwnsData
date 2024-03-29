﻿using System.Threading.Tasks;
using System.Web.Mvc;
using DucksApp.Services.PowerBI;

namespace DucksApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IEmbedService embedService;

        public HomeController()
        {
            embedService = new EmbedService();
        }

        public async Task<ActionResult> Index()
        {
            await embedService.SetReportEmbedConfigAsync();

            return View(embedService.EmbedConfig);
        }

        [HttpGet]
        public async Task<JsonResult> RefreshDataSet()
        {
            var test = await embedService.RefreshDatasetAsync();
            return Json(test, JsonRequestBehavior.AllowGet);
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}