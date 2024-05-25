using BlinkBackend.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Cors;

namespace BlinkBackend.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class AdminController : ApiController
    {
        readonly BlinkMovieEntities db = new BlinkMovieEntities();

        /* [HttpPut]
         public HttpResponseMessage AcceptBalanceRequest(int Reader_ID)
         {

                 var balanc = db.BalanceRequests.FirstOrDefault(r => r.Reader_ID == Reader_ID);
                 var reader =db.Reader.FirstOrDefault(r => r.Reader_ID == Reader_ID);    

                 if (balanc != null)
                 {
                     reader.Balance += balanc.Balance;
                     db.SaveChanges();

                     return Request.CreateResponse(HttpStatusCode.OK, "Balance updated successfully");
                 }
                 else
                 {
                     return Request.CreateResponse(HttpStatusCode.NotFound, "User not found");
                 }

         }*/
        [HttpGet]
        public HttpResponseMessage GetBalanceRequests()
        {
            using (var db = new BlinkMovieEntities())
            {
                var balanceRequests = db.BalanceRequests
                    .Where(br => br.Status == "Sent")
                    .Select(br => new
                    {
                        br.Balance_ID,
                        br.Balance,
                        br.RequestDate,
                        br.Status,
                        ReaderDetails = db.Reader
                            .Where(r => r.Reader_ID == br.Reader_ID)
                            .Select(r => new
                            {
                                r.UserName,
                                r.Email,
                                r.Image
                            })
                            .FirstOrDefault()
                    })
                    .AsEnumerable() 
                    .OrderByDescending(br => DateTime.Parse(br.RequestDate)) 
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, balanceRequests);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetAdminNotificationCount()
        {
            try
            {
                using (var db = new BlinkMovieEntities())
                {
                    db.Configuration.LazyLoadingEnabled = false;
                    db.Configuration.ProxyCreationEnabled = false;

                    var notificationCount = db.BalanceRequests
                                              .Count(br => br.adminNotifications == true);
                    var notification = db.BalanceRequests
                                         .Where(br => br.adminNotifications == true)
                                         .Select(s => new
                                         {

                                             Balance_ID = s.Balance_ID
                                         })
                                         .ToList(); 

                    var response = new
                    {
                        count = notificationCount,
                        Notification = notification
                    };

                    return Request.CreateResponse(HttpStatusCode.OK, response);
                }
            }
            catch (Exception ex)
            {
                // Log the exception details (ex)
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPut]

        public HttpResponseMessage ResetAdminNotifications()
        {
            using (var db = new BlinkMovieEntities())
            {
                var balanceRequests = db.BalanceRequests.Where(br => br.adminNotifications == true);

                foreach (var request in balanceRequests)
                {
                    request.adminNotifications = false;
                }

                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, "Admin notifications reset successfully.");
            }
        }

        [HttpPost]
        public HttpResponseMessage RejectBalanceRequest(int Balance_ID)
        {
            using (var db = new BlinkMovieEntities())
            {
                var balanceRequest = db.BalanceRequests.FirstOrDefault(br => br.Balance_ID == Balance_ID);

                if (balanceRequest != null)
                {
                    balanceRequest.Status = "Rejected"; 
                    db.SaveChanges();
                    return Request.CreateResponse(HttpStatusCode.OK, "Balance request rejected successfully.");
                }
                else
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Balance request not found.");
                }
            }
        }

        [HttpPut]

        public HttpResponseMessage AcceptBalanceRequest(int id)
        {
            using (var db = new BlinkMovieEntities())
            {
                var balanceRequest = db.BalanceRequests.FirstOrDefault(br => br.Balance_ID == id);

                if (balanceRequest == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Balance request not found.");
                }

                if (balanceRequest.Status == "Accepted")
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Balance request is already accepted.");
                }

                var reader = db.Reader.FirstOrDefault(r => r.Reader_ID == balanceRequest.Reader_ID);

                if (reader == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Reader not found.");
                }


                reader.Subscription = "Paid";
                reader.Balance = (reader.Balance ?? 0) + balanceRequest.Balance;



                balanceRequest.Status = "Accepted";


                db.SaveChanges();

                Task.Run(async () => await ResetBalanceAndSubscription(reader.Reader_ID));


                return Request.CreateResponse(HttpStatusCode.OK, "Balance request accepted and reader's balance updated.");
            }
        }

        private async Task<HttpResponseMessage> ResetBalanceAndSubscription(int Reader_ID)
        {
            using (BlinkMovieEntities db = new BlinkMovieEntities())
            {
                //  await Task.Delay(TimeSpan.FromDays(1));
                await Task.Delay(TimeSpan.FromSeconds(120));
                try
                {
                    var reader = db.Reader.FirstOrDefault(r => r.Reader_ID == Reader_ID);
                    if (reader != null)
                    {
                        reader.Balance = 0;
                        reader.Subscription = "Free";
                        db.SaveChanges();
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("Balance and subscription reset successfully.")
                        };
                    }
                    else
                    {
                        return new HttpResponseMessage(HttpStatusCode.NotFound)
                        {
                            Content = new StringContent("Reader not found.")
                        };
                    }
                }
                catch (Exception ex)
                {
                    
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent($"An error occurred: {ex.Message}")
                    };
                }
            }
        }

        [HttpDelete]
        public HttpResponseMessage DeleteUser(int id, string role)
        {
            try
            {
                switch (role.ToLower())
                {
                    case "writer":
                        var writer = db.Writer.FirstOrDefault(w => w.Writer_ID == id);
                        if (writer == null)
                        {
                            return Request.CreateResponse(HttpStatusCode.NotFound, "Writer not found");
                        }

                        db.Writer.Remove(writer);
                        db.SaveChanges();

                        return Request.CreateResponse(HttpStatusCode.OK, "Writer successfully deleted");

                    case "reader":
                        var reader = db.Reader.FirstOrDefault(r => r.Reader_ID == id);
                        if (reader == null)
                        {
                            return Request.CreateResponse(HttpStatusCode.NotFound, "Reader not found");
                        }

                        db.Reader.Remove(reader);
                        db.SaveChanges();

                        return Request.CreateResponse(HttpStatusCode.OK, "Reader successfully deleted");

                    case "editor":
                        var editor = db.Editor.FirstOrDefault(e => e.Editor_ID == id);
                        if (editor == null)
                        {
                            return Request.CreateResponse(HttpStatusCode.NotFound, "Editor not found");
                        }

                        db.Editor.Remove(editor);
                        db.SaveChanges();

                        return Request.CreateResponse(HttpStatusCode.OK, "Editor successfully deleted");

                    default:
                        return Request.CreateResponse(HttpStatusCode.BadRequest, "Invalid role specified");
                }
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        public HttpResponseMessage AddEditor(Editor editorForm)
        {
            try
            {
                
                var newEditor = new Editor
                {
                    Email = editorForm.Email,
                    Password = editorForm.Password,
                    UserName = editorForm.UserName
                };

                db.Editor.Add(newEditor);
                db.SaveChanges();

                
                var newEditorUser = new Users
                {
                    Editor_ID = newEditor.Editor_ID,
                    Email = editorForm.Email,
                    Password = editorForm.Password,
                    Role = "editor", 
                };

                db.Users.Add(newEditorUser);
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.Created, "Editor successfully added");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
    }
}
