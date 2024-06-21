using BlinkBackend.Models;
using BlinkBackend.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Cors;

namespace BlinkBackend.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class WriterController : ApiController
    {

        static int GenerateId()
        {

            long timestamp = DateTime.Now.Ticks;
            Random random = new Random();
            int randomComponent = random.Next();

            int userId = (int)(timestamp ^ randomComponent);

            return Math.Abs(userId);
        }
        [HttpPost]
        public HttpResponseMessage RejectProposal(int SentProposals_ID)
        {
            try
            {
                BlinkMovieEntities db = new BlinkMovieEntities();
                var Proposal = db.SentProposals.FirstOrDefault(r => r.SentProposal_ID == SentProposals_ID);

                if (Proposal != null)
                {
                   Proposal.Status = "Rejected";
                    Proposal.Editor_Notification = true;
                    db.SaveChanges();
                    return Request.CreateResponse(HttpStatusCode.OK, "Proposal Rejected");
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Proposal not found");
                }
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        [HttpGet]
        public HttpResponseMessage ViewRewriteProject(int Writer_ID)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();
            var projects = db.SentProject
                .Where(s => s.Status == "Rewrite" && s.Writer_ID==Writer_ID)
                .Select(s => new
                {
                    s.Movie_ID,
                    s.SentProject_ID,
                    s.SentProposal_ID,
                    s.Editor_ID,
                    s.Writer_ID,
                    SentProposalData = db.SentProposals
                        .Where(sp => sp.SentProposal_ID == s.SentProposal_ID)
                        .Select(sp => new
                        {
                            sp.SentProposal_ID,
                            sp.Movie_Name,
                            sp.Image,
                            sp.Genre,
                            sp.Type,
                            sp.Episode,
                            sp.Director,
                            sp.DueDate
                        })
                        .FirstOrDefault()
                })
                .ToList();

        

            return Request.CreateResponse(HttpStatusCode.OK, projects);
        }

       

        [HttpGet]
        public HttpResponseMessage GetWriterRating(int writerId)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();
            var writerRating = db.Writer.Where(w => w.Writer_ID == writerId).Select(s => s.AverageRating).FirstOrDefault();
            return Request.CreateResponse(HttpStatusCode.OK, writerRating);
        }
            [HttpPost]
        public HttpResponseMessage AcceptProposal(int SentProposals_ID)
        {
            try
            {
                BlinkMovieEntities db = new BlinkMovieEntities();
                var acceptProposal = db.SentProposals.FirstOrDefault(r => r.SentProposal_ID == SentProposals_ID);

                if (acceptProposal != null)
                {
                    acceptProposal.Status = "Accepted";
                    acceptProposal.Editor_Notification = true;
                    db.SaveChanges();
                    return Request.CreateResponse(HttpStatusCode.OK, "Proposal Accepted");
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Proposal not found");
                }
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetRewriteData(int SentProject_ID)
        {
            try
            {
                BlinkMovieEntities db = new BlinkMovieEntities();

                
                db.Configuration.LazyLoadingEnabled = false;


                var sentProjects = db.SentProject
                    .Where(sp => sp.SentProject_ID == SentProject_ID)
                    .FirstOrDefault();


                var summaries = db.Summary.FirstOrDefault(s=> s.Sent_ID == SentProject_ID);




                object result = new
                {
                    SentProjects = sentProjects,
                    Summaries = summaries,
                  
                };


               

                return  Request.CreateResponse(HttpStatusCode.OK, result) ;
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        [HttpGet]
        public HttpResponseMessage ShowProposals(int Writer_ID)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();
            try
            {
                var proposals = db.SentProposals
                                    .Where(s => s.Writer_ID == Writer_ID && s.Status == "Sent")
                                    .OrderByDescending(s => s.Sent_at)
                                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, proposals);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

       

        [HttpPost]
        public HttpResponseMessage SentProject(SentProjects spro)
        {

            BlinkMovieEntities db = new BlinkMovieEntities();
            DateTime currentDate = DateTime.Now;
            double currentSeconds = currentDate.TimeOfDay.TotalSeconds;

            var proposal = db.SentProposals.Where(s => s.SentProposal_ID == spro.SentProposal_ID).FirstOrDefault();
            try
            {
                 proposal.Status = "Received";
                 db.SaveChanges();

                var project = new SentProject()
                {
                    SentProject_ID = GenerateId(),
                    Movie_ID = proposal.Movie_ID,
                    SentProposal_ID = spro.SentProposal_ID,
                    Editor_ID = proposal.Editor_ID,
                    Writer_ID = spro.Writer_ID,
                    Editor_Notification = true,
                    Send_at = currentDate.ToString(),
                    Status = "Sent"
                };
                db.SentProject.Add(project);
                db.SaveChanges();

               
                var summary = new Summary()
                {
                    Summary_ID = GenerateId(),
                    Sent_ID = project.SentProject_ID,
                    Writer_ID = spro.Writer_ID,
                    Summary1 = spro.Summary,
                    Episode = spro.Episode
                };

                db.Summary.Add(summary);
                db.SaveChanges();

               if(spro.Type == "Movie")
                {
                    foreach (var clip in spro.Clips)
                    {
                        var newClip = new Clips()
                        {
                            Clips_ID = GenerateId(),
                            Sent_ID = project.SentProject_ID,
                            Writer_ID = proposal.Writer_ID,
                            Url = clip.Url,
                            Title = clip.Title,
                            isCompoundClip = clip.isCompoundClip,
                            Start_time = clip.Start_Time,
                            End_time = clip.End_Time,
                            DateAdded = currentSeconds.ToString(),
                        };

                        db.Clips.Add(newClip);
                        db.SaveChanges();
                    }
                }
                else
                {
                    foreach (var clip in spro.Clips)
                    {
                        var newDramasClip = new DramasClips()
                        {
                            DramasClip_ID = GenerateId(),
                            Sent_ID = project.SentProject_ID,
                            Writer_ID = proposal.Writer_ID,
                            Url = clip.Url,
                            Title = clip.Title,
                            isCompoundClip = clip.isCompoundClip,
                            Start_time = clip.Start_Time,
                            End_time = clip.End_Time,
                            Episode = clip.Episode,
                            DateAdded = currentSeconds.ToString(),
                        };

                        db.DramasClips.Add(newDramasClip);
                        db.SaveChanges();
                    }
                }

               

                return Request.CreateResponse(HttpStatusCode.OK,"Project Sent");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(ex);
            }

        }

        [HttpGet]
        public HttpResponseMessage GetWriterSentProjects(int writerId)
        {
            try
            {
                BlinkMovieEntities db = new BlinkMovieEntities();

                var sentProjects = (from sp in db.SentProject
                                    where sp.Writer_ID == writerId
                                    join m in db.Movie on sp.Movie_ID equals m.Movie_ID
                                    join w in db.Writer on sp.Writer_ID equals w.Writer_ID
                                    join s in db.SentProposals on sp.SentProposal_ID equals s.SentProposal_ID
                                    select new
                                    {
                                        Writer_ID = sp.Writer_ID,
                                        SentProject_ID = sp.SentProject_ID,
                                        Genre = m.Category,
                                        Type = m.Type,
                                        Writer_Name = w.UserName,
                                        Status = sp.Status,
                                        Movie_Name = m.Name,
                                        Image = m.Image,
                                        Episode = s.Episode,
                                        Balance =s.Balance,
                                        Director = m.Director,
                                        Sent_at =sp.Send_at
                                    }).ToList();

                return Request.CreateResponse(HttpStatusCode.OK, sentProjects);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        [HttpGet]
        public HttpResponseMessage ShowWriterRating(int Writer_ID)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();
            try
            {
                var rating = db.Writer.Where(s => s.Writer_ID == Writer_ID).FirstOrDefault();


                return Request.CreateResponse(HttpStatusCode.OK, rating.Rating);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

      


        [HttpGet]
        public HttpResponseMessage AcceptedProposals(int Writer_ID)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();
            try
            {
                var proposals = db.SentProposals
                    .Where(s => s.Writer_ID == Writer_ID && s.Status == "Accepted")
                    .Select(s => new
                    {
                        s.SentProposal_ID,
                        s.Editor_ID,
                        s.Writer_ID,
                        s.Movie_ID,
                        s.Movie_Name,
                        s.Type,
                        s.Image,
                        s.Genre,
                        s.Balance,
                        s.Episode,
                        s.DueDate
                    })
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, proposals);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetSpecificProposal(int SentProposals_ID)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();
            db.Configuration.LazyLoadingEnabled = false;

            
            var proposal = db.SentProposals.FirstOrDefault(s => s.SentProposal_ID == SentProposals_ID);

            if (proposal != null)
            {
                
                var editor = db.Editor.FirstOrDefault(e => e.Editor_ID == proposal.Editor_ID);

               
                var movie = db.Movie.FirstOrDefault(m => m.Movie_ID == proposal.Movie_ID);

                if (editor != null && movie != null)
                {
                    
                    var result = new
                    {
                        Proposal = proposal,
                        Editor = new
                        {
                            UserName = editor.UserName,
                            Email = editor.Email
                        },
                        MovieDescription = movie.Description
                    };

                    return Request.CreateResponse(HttpStatusCode.OK, result);
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Editor or Movie not found");
                }
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, "Data not found");
            }
        }


        [HttpGet]
        public HttpResponseMessage GetWriterAccordingToGenre(string movieGenre)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();
            try
            {
                
               if(movieGenre != null)
                {
                    string[] categories = movieGenre.Split(',');

                    
                    if (categories != null)
                    {
                        var writers = db.Writer.ToList() 
    .Where(writer =>
        categories.Any(category =>
            writer.Interest?.Split(',')?.Contains(category.Trim()) ?? false
        )
    )
    .ToList();
                       
                        if (!writers.Any())
                        {
                            return Request.CreateResponse(HttpStatusCode.NotFound, "No writers found with matching interests.");
                        }

                        
                        var writerC = writers.Select(writer => new
                        {
                            WriterID = writer.Writer_ID,
                            UserName = writer.UserName,
                        }).ToList();

                        return Request.CreateResponse(HttpStatusCode.OK, writerC);
                    }

                    else
                    {
                        return null;
                    }
                }

                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                // Return internal server error if an exception occurs
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        [HttpGet]

        public HttpResponseMessage GetSpecificWriter(int Writer_ID)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();
            db.Configuration.LazyLoadingEnabled = false;

            var writer = db.Writer.FirstOrDefault(w => w.Writer_ID == Writer_ID);
            if (writer != null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, writer);
            }

            else
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, "Data not found");
            }

        }

        [HttpGet]
        public HttpResponseMessage GetUpdateSummaryDetails(int sentProjectId)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();

            try
            {
                
                var summaryData = db.Summary.FirstOrDefault(s => s.Sent_ID == sentProjectId);

                if (summaryData == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Summary not found");
                }


                var query = from sentProject in db.SentProject
                            join movie in db.Movie on sentProject.Movie_ID equals movie.Movie_ID
                            where sentProject.SentProject_ID == sentProjectId
                            select new
                            {
                                MovieName = movie.Name 
                            };

                var movieData = query.FirstOrDefault();

                if (movieData == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Movie not found");
                }


                var responseData = new
                {
                    SummaryData = summaryData.Summary1,
                    MovieName = movieData.MovieName
                };

                return Request.CreateResponse(HttpStatusCode.OK, responseData);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        [HttpPut]
        public HttpResponseMessage UpdateSummary(int sentProjectId, string newSummary)
        {
            DateTime currentDate = DateTime.Now;
            BlinkMovieEntities db = new BlinkMovieEntities();

            try
            {
                var summaryToUpdate = db.Summary.FirstOrDefault(s => s.Sent_ID == sentProjectId);

                if (summaryToUpdate == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Summary not found");
                }

                var sentProjectTableUpdate = db.SentProject.FirstOrDefault(s => s.SentProject_ID == sentProjectId);


                summaryToUpdate.Summary1 = newSummary;
                sentProjectTableUpdate.Status = "Sent";
                sentProjectTableUpdate.Send_at = currentDate.ToString();
                sentProjectTableUpdate.Editor_Notification = true;

                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, "Summary updated successfully");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        [HttpPost]
        public HttpResponseMessage UpdateWriterNotifications(int writerId)
        {
            try
            {
                BlinkMovieEntities db = new BlinkMovieEntities();

                
                var sentProposals = db.SentProposals.Where(sp => sp.Writer_ID == writerId).ToList();

                if (sentProposals.Count == 0)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No SentProposals found for the specified writer");
                }

                foreach (var sentProposal in sentProposals)
                {
                    sentProposal.Writer_Notification = false;
                }

               
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, "Writer notifications updated successfully");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetSentProposalsIdsWithWriterNotification(int writerId)
        {
            try
            {
                BlinkMovieEntities db = new BlinkMovieEntities();

               
                var sentProposalIds = db.SentProposals
                                        .Where(sp => sp.Writer_ID == writerId && sp.Writer_Notification==true)
                                        .Select(sp => new
                                        {
                                            sp.SentProposal_ID,
                                            sp.Status
                                        })
                                        .ToList();

                if (sentProposalIds.Count == 0)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No SentProposals found with Writer_Notification true for the specified writer");
                }

               
                int totalCount = sentProposalIds.Count;

               
                var responseData = new
                {
                    SentProposalIds = sentProposalIds,
                    TotalCount = totalCount
                };

                return Request.CreateResponse(HttpStatusCode.OK, responseData);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        public HttpResponseMessage UpdateAllWriterNotificationstoFalse(int writerId)
        {
            try
            {
                BlinkMovieEntities db = new BlinkMovieEntities();

               
                var sentProposals = db.SentProposals.Where(sp => sp.Writer_ID == writerId).ToList();

                if (sentProposals.Count == 0)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No SentProposals found for the specified writer");
                }

               
                foreach (var sentProposal in sentProposals)
                {
                    sentProposal.Writer_Notification = false;
                }

                
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, "All Writer notifications updated successfully");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetWriterNotificationsSentProject(int writerId)
        {
            try
            {
                BlinkMovieEntities db = new BlinkMovieEntities();

                
                var writerNotifications = db.SentProject
                                            .Where(sp => sp.Writer_Notification ==true && sp.Writer_ID == writerId)
                                            .Select(sp => new
                                            {
                                                SentProject_ID = sp.SentProject_ID,
                                                Status = sp.Status
                                            })
                                            .ToList();

                if (writerNotifications.Count == 0)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No writer notifications found for the specified writer");
                }

                
                int totalCount = writerNotifications.Count;

               
                var responseData = new
                {
                    WriterNotifications = writerNotifications,
                    TotalCount = totalCount
                };

                return Request.CreateResponse(HttpStatusCode.OK, responseData);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        public HttpResponseMessage UpdateAllWriterNotificationsToFalseSentProject(int writerId)
        {
            try
            {
                BlinkMovieEntities db = new BlinkMovieEntities();

                
                var sentProjects = db.SentProject.Where(sp => sp.Writer_ID == writerId).ToList();

                if (sentProjects.Count == 0)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No SentProject records found for the specified writer");
                }

                foreach (var sentProject in sentProjects)
                {
                    sentProject.Writer_Notification = false;
                }

               
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, "All Writer notifications updated to false for the specified writer");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetInterests()
        {
            try
            {
                BlinkMovieEntities db = new BlinkMovieEntities();


                var interests = db.Interests.Where(i => i.Interest_ID>0).Select(s=>s.Interests1).ToList();

                if (interests.Count == 0)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No Interest records found");
                }

                return Request.CreateResponse(HttpStatusCode.OK, interests);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


    }


}


//Accepted projects
//Self Write