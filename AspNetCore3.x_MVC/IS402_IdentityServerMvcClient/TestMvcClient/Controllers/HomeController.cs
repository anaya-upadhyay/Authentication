﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TestMvcClient.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [Authorize] // Requires the Cookie using OIDC Schema
        public IActionResult Profile()
        {
            return View();
        }
    }
}