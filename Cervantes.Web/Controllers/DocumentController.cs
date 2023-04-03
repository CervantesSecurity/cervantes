﻿using Cervantes.Contracts;
using Cervantes.CORE;
using Cervantes.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Web;
using Cervantes.Web.Areas.Workspace.Models;
using Ganss.XSS;
using MimeDetective;

namespace Cervantes.Web.Controllers;
[Authorize(Roles = "Admin,SuperUser,User")]
public class DocumentController : Controller
{
    private readonly ILogger<DocumentController> _logger = null;
    private readonly IHostingEnvironment _appEnvironment;
    private IDocumentManager documentManager = null;

    public DocumentController(IDocumentManager documentManager, ILogger<DocumentController> logger,
        IHostingEnvironment _appEnvironment)
    {
        this.documentManager = documentManager;
        this._appEnvironment = _appEnvironment;
        _logger = logger;
    }

    // GET: DocumentController
    public ActionResult Index()
    {
        try
        {
            var model = documentManager.GetAll().Select(e => new Document
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                FilePath = e.FilePath,
                User = e.User,
                UserId = e.UserId
            });

            if (model != null)
            {
                return View(model);
            }
            else
            {
                TempData["empty"] = "No clients introduced";
                return View();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error ocurred loading Document Index. User: {0}",
                User.FindFirstValue(ClaimTypes.Name));
            return RedirectToAction("Error","Home");
        }
    }

    public IActionResult Details(Guid id)
    {
        try
        {
            var doc = documentManager.GetById(id);
            var model = new Document
            {
                Id = doc.Id,
                Name = doc.Name,
                Description = doc.Description,
                UserId = doc.UserId,
                User = doc.User,
                FilePath = doc.FilePath,
                Visibility = doc.Visibility,
                CreatedDate = doc.CreatedDate.ToUniversalTime()
            };
            return View(model);
        }
        catch (Exception e)
        {
            TempData["errorLoadingDocument"] = "Error loading document!";

            _logger.LogError(e, "An error ocurred loading Document Details. User: {0}. Document: {1}",
                User.FindFirstValue(ClaimTypes.Name), id);
            return RedirectToAction("Index");
        }
    }

    // GET: DocumentController/Create
    public ActionResult Create()
    {
        return View();
    }

    // POST: DocumentController/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,SuperUser")]
    public ActionResult Create(DocumentViewModel model, IFormFile Upload)
    {
        try
        {
            var file = Upload;
            var sanitizer = new HtmlSanitizer();
            sanitizer.AllowedSchemes.Add("data");


            if (file != null)
            {
                var Inspector = new ContentInspectorBuilder() {
                    Definitions = MimeDetective.Definitions.Default.FileTypes.Documents.All()
                }.Build();
            
                var Results = Inspector.Inspect(file.OpenReadStream());

                if (Results.ByFileExtension().Length == 0 && Results.ByMimeType().Length == 0)
                {
                    TempData["fileNotPermitted"] = "User is not in the project";
                        return View("Create");
                }

                
                var uploads = Path.Combine(_appEnvironment.WebRootPath, "Attachments/Documents");
                var uniqueName = Guid.NewGuid().ToString() + "_" + file.FileName;
                using (var fileStream = new FileStream(Path.Combine(uploads, uniqueName), FileMode.Create))
                {
                    file.CopyTo(fileStream);
                }

                var doc = new Document
                {
                    Name = model.Name,
                    Description = sanitizer.Sanitize(HttpUtility.HtmlDecode(model.Description)),
                    FilePath = "/Attachments/Documents/" + uniqueName,
                    CreatedDate = DateTime.Now.ToUniversalTime(),
                    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    Visibility = Visibility.Public
                };
                documentManager.AddAsync(doc);
                documentManager.Context.SaveChanges();
                TempData["createdDocument"] = "created";
                _logger.LogInformation("User: {0} Created a new Document: {1}", User.FindFirstValue(ClaimTypes.Name),
                    doc.Name);
                return RedirectToAction("Details", "Document", new {id = doc.Id});
            }
            else
            {
                /*Document doc = new Document
                {
                    Name = model.Name,
                    Description = model.Description,
                    CreatedDate = DateTime.Now.ToUniversalTime(),
                    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    Visibility = Visibility.Public
                };
                documentManager.AddAsync(doc);
                documentManager.Context.SaveChanges();
                TempData["created"] = "created";
                _logger.LogInformation("User: {0} Created a new Document: {1}", User.FindFirstValue(ClaimTypes.Name), doc.Name);*/
                return View(model);
            }
        }
        catch (Exception ex)
        {
            TempData["errorCreatingDocument"] = "Error creating document!";

            _logger.LogError(ex, "An error ocurred adding a new Document. User: {0}",
                User.FindFirstValue(ClaimTypes.Name));
            return View("Create");
        }
    }

    [Authorize(Roles = "Admin,SuperUser")]
    public ActionResult Edit(Guid id)
    {
        try
        {
            //obtenemos la categoria a editar mediante su id
            var result = documentManager.GetById(id);

            var doc = new Document
            {
                Name = result.Name,
                Description = result.Description
            };
            return View(doc);
        }
        catch (Exception ex)
        {
            TempData["errorLoadingDocument"] = "Error loading document!";

            _logger.LogError(ex, "An error ocurred loading edit form on Document Id: {0}. User: {1}", id,
                User.FindFirstValue(ClaimTypes.Name));
            return RedirectToAction("Index");
        }
    }

    // POST: DocumentController/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,SuperUser")]
    public ActionResult Edit(Guid id, Document model)
    {
        try
        {
            var sanitizer = new HtmlSanitizer();
            sanitizer.AllowedSchemes.Add("data");

            var result = documentManager.GetById(id);
            result.Name = model.Name;
            result.Description = sanitizer.Sanitize(HttpUtility.HtmlDecode(model.Description));

            documentManager.Context.SaveChanges();
            TempData["editedDocument"] = "edited";
            _logger.LogInformation("User: {0} edited Document: {1}", User.FindFirstValue(ClaimTypes.Name), result.Name);
            return RedirectToAction("Details", "Document", new {id = id});
        }
        catch (Exception ex)
        {
            TempData["errorEditingDocument"] = "Error editing document!";

            _logger.LogError(ex, "An error ocurred editing Document Id: {0}. User: {1}", id,
                User.FindFirstValue(ClaimTypes.Name));
            return RedirectToAction("Edit", "Document", new {id = id});
        }
    }

    // GET: DocumentController/Delete/5
    public ActionResult Delete(Guid id)
    {
        try
        {
            var doc = documentManager.GetById(id);
            if (doc != null)
            {
                var document = new Document
                {
                    Id = doc.Id,
                    Name = doc.Name,
                    Description = doc.Description
                };

                return View(document);
            }
        }
        catch (Exception e)
        {
            TempData["errorLoadingDocument"] = "Error loading document!";

            _logger.LogError(e, "An error ocurred loading delet form on Document Id: {0}. User: {1}", id,
                User.FindFirstValue(ClaimTypes.Name));
            return RedirectToAction("Index");
        }

        return View();
    }

    // POST: DocumentController/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,SuperUser")]
    public ActionResult Delete(Guid id, IFormCollection collection)
    {
        try
        {
            var doc = documentManager.GetById(id);
            if (doc != null)
            {
                documentManager.Remove(doc);
                documentManager.Context.SaveChanges();
            }

            TempData["deletedDocument"] = "deleted";
            _logger.LogInformation("User: {0} deleted document: {1}", User.FindFirstValue(ClaimTypes.Name), doc.Name);
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            TempData["errorDeletingDocument"] = "Error deleting document!";
            _logger.LogError(ex, "An error ocurred deleteing Document Id: {0}. User: {1}", id,
                User.FindFirstValue(ClaimTypes.Name));
            return View();
        }
    }
}