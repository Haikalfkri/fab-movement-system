using System.Web.Mvc;
using System;
using ok1.Data;
using System.Linq;
using ok1.Models;
using System.Web;
using System.Net;
using System.Web.Routing;

public class FeatureAuthorizeAttribute : ActionFilterAttribute
{
    private readonly string _pageName;

    public FeatureAuthorizeAttribute(string pageName)
    {
        _pageName = pageName;
    }
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        // Ambil username dari HttpContext
        var currentUser = filterContext.HttpContext.User.Identity.Name;
        System.Diagnostics.Debug.WriteLine($"Current User: {currentUser}");

        using (var db = new ok1Context())
        {
            var credential = db.Credentials.FirstOrDefault(c => c.WindAcc.ToLower() == currentUser.ToLower());

            if (credential == null)
            {
                filterContext.Result = new HttpStatusCodeResult(403, "Access Denied - User not found");
                return;
            }

            if (credential.isAdmin)
            {
                System.Diagnostics.Debug.WriteLine($"Admin access granted for: {currentUser}");
                return;
            }
            if (_pageName.ToLower() == "landing")
            {
                System.Diagnostics.Debug.WriteLine($"Non-admin user {currentUser} accessing Landing page, redirecting to Users/Index");
                filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary
                {
                    { "controller", "Users"}, 
                    { "action", "Index" }
                });
                return;
            }

            // Jika pengguna bukan admin, periksa izin berbasis halaman
            bool hasAccess = db.PageAccessUsers.Any(p =>
                p.CredentialId == credential.Id &&
                p.PageName.ToLower() == _pageName.ToLower());

            if (!hasAccess)
            {
                System.Diagnostics.Debug.WriteLine($"Access denied for user: {currentUser} on page: {_pageName}");
                filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary
                {
                    { "controller", "Users"},
                    { "action", "Index" }
                });
                return;
            }
        }
    }
}