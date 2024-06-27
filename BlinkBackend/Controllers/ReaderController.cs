using BlinkBackend.Models;
using BlinkBackend.Classes;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web;
using System.Web.Http.Cors;
using Newtonsoft.Json;
using System.Text;

namespace BlinkBackend.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class ReaderController : ApiController
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

        public HttpResponseMessage AddReaderFavorites(Favorites f)
        {

            BlinkMovieEntities db = new BlinkMovieEntities();
            try
            {


                var favorites = new Favorites()
                {
                    Favorites_ID = GenerateId(),
                    Reader_ID = f.Reader_ID,
                    Writer_ID = f.Writer_ID,
                    Movie_ID = f.Movie_ID,
                    Episode=f.Episode,
                };

                db.Favorites.Add(favorites);
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, "Added To Favorites");


            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpPost]

        public HttpResponseMessage RemovefromFavorites(int Favorites_ID)
        {
            try
            {
                BlinkMovieEntities db = new BlinkMovieEntities();

               var favorite =  db.Favorites.Where(f => f.Favorites_ID == Favorites_ID).FirstOrDefault();

                db.Favorites.Remove(favorite);
                db.SaveChanges();


                return Request.CreateResponse(HttpStatusCode.OK, "Removed from Favorites");

            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

            [HttpGet]
        public HttpResponseMessage GetReaderFavoriteMovies(int readerId)
        {
            using (BlinkMovieEntities db = new BlinkMovieEntities())
            {
                try
                {
                    var favoriteMovies = db.Favorites
                        .Where(f => f.Reader_ID == readerId)
                        .Join(db.Movie,
                              f => f.Movie_ID,
                              m => m.Movie_ID,
                              (f, m) => new { f, m })
                        .Join(db.Writer,
                              fm => fm.f.Writer_ID,
                              w => w.Writer_ID,
                              (fm, w) => new
                              {
                                  Favorites_ID = fm.f.Favorites_ID,
                                  MovieId = fm.m.Movie_ID,
                                  MovieName = fm.m.Name,
                                  MovieImage = fm.m.Image,
                                  Writer_ID = w.Writer_ID,
                                  Type = fm.m.Type,
                                  Episode= fm.f.Episode,
                                  WriterName = w.UserName,
                                  WriterImage = w.Image,
                                  isFavorited = true
                              }).Distinct()
                        .ToList();

                    if (favoriteMovies == null || !favoriteMovies.Any())
                    {
                        return Request.CreateResponse(HttpStatusCode.NotFound, "No favorite movies found for the given reader.");
                    }

                    return Request.CreateResponse(HttpStatusCode.OK, favoriteMovies);
                }
                catch (Exception ex)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
                }
            }
        }


        [HttpPost]

        public HttpResponseMessage AddReaderHistory(History h)
        {

            BlinkMovieEntities db = new BlinkMovieEntities();
            try
            {

                var history = new History()
                {
                    History_ID = GenerateId(),
                    Reader_ID = h.Reader_ID,
                    Writer_ID = h.Writer_ID,
                    Movie_ID = h.Movie_ID,
                };

                db.History.Add(history);
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, "Added To History");


            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpGet]
        public HttpResponseMessage IssueFreeMovie(int readerId)
        {
            using (BlinkMovieEntities db = new BlinkMovieEntities())
            {
                var jsonSettings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };
                db.Configuration.LazyLoadingEnabled = false;
                try
                {

                    var lastIssuedDate = db.FreeMovie
                                    .Where(fm => fm.Reader_ID == readerId)
                                    .OrderByDescending(fm => fm.issueDate)
                                    .Select(fm => fm.issueDate)
                                    .FirstOrDefault();

                    string todayDateString = DateTime.Today.ToString("yyyy-MM-dd");
                    bool hasDayPassed = (lastIssuedDate != null && DateTime.Parse(lastIssuedDate) < DateTime.Today);





                    var readerIssuedFreeMovie = db.FreeMovie.Where(fm => fm.Reader_ID == readerId).ToList();
                    


                     if (hasDayPassed || lastIssuedDate==null)
                    {
                      


                    again:
                       var  randomSummary = db.Summary
                                              .OrderBy(r => Guid.NewGuid())
                                              .FirstOrDefault();


                        if (lastIssuedDate == null)
                        {
                            randomSummary = db.Summary
                                              .OrderBy(r => Guid.NewGuid())
                                              .FirstOrDefault();

                            if (randomSummary == null)
                            {
                                return Request.CreateResponse(HttpStatusCode.NotFound, "No available summaries to issue.");
                            }
                        }

                        if (randomSummary.Movie_ID == null)
                        {
                            goto again;
                        }
                       
                        if(readerIssuedFreeMovie.Count == db.Summary.Count())
                        {
                            var recordsToDelete = db.FreeMovie.Where(fm => fm.Reader_ID == readerId);
                            
                            db.FreeMovie.RemoveRange(recordsToDelete);
                            
                            db.SaveChanges();
                        }

                        foreach (var issuedMovie in readerIssuedFreeMovie)
                        {
                            if (issuedMovie.Movie_ID == randomSummary.Movie_ID && issuedMovie.Writer_ID == randomSummary.Writer_ID )
                            {

                                goto again;
                            }
                        }

                        var movie = db.Movie.Where(m => m.Movie_ID == randomSummary.Movie_ID).Select(s => new
                        {
                            s.Movie_ID,
                            s.Name,
                            s.Image,
                            s.CoverImage,
                            s.Type,
                            s.Category,
                            s.Director
                        }).FirstOrDefault();


                        var writer = db.Writer.Where(w => w.Writer_ID == randomSummary.Writer_ID).Select(s => new
                        {
                            s.Writer_ID,
                            s.UserName,
                           
                        }).FirstOrDefault();


                        FreeMovie newFreeMovie = new FreeMovie
                        {
                            FreeMovie_ID = GenerateId(),
                            Movie_ID = randomSummary.Movie_ID,
                            Writer_ID = randomSummary.Writer_ID,
                            issueDate = todayDateString,
                            Reader_ID = readerId,
                            Episode = randomSummary.Episode,
                        };

                        db.FreeMovie.Add(newFreeMovie);
                        db.SaveChanges();


                        var result = new
                        {
                            Movie = movie,
                            Writer = writer,
                            Episode= randomSummary.Episode,
                            IssuedMovie = newFreeMovie
                        };


                        string resultJson = JsonConvert.SerializeObject(result, jsonSettings);

                        HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.OK);
                        response.Content = new StringContent(resultJson, Encoding.UTF8, "application/json");

                        return response;
                    }
                    else
                    {
                        
                        var lastIssuedMovie = db.FreeMovie
                                                .Where(fm => fm.Reader_ID == readerId && fm.issueDate == lastIssuedDate)
                                                .FirstOrDefault();
                        
                       var  movie = db.Movie.Where(m => m.Movie_ID == lastIssuedMovie.Movie_ID).Select(s => new
                       {
                           s.Movie_ID,
                           s.Name,
                           s.Image,
                           s.CoverImage,
                           s.Type,
                           s.Category,
                           s.Director
                       }).FirstOrDefault();


                        var writer = db.Writer.Where(w => w.Writer_ID == lastIssuedMovie.Writer_ID).Select(s => new
                        {
                            s.Writer_ID,
                            s.UserName,
                        }).FirstOrDefault();




                          var result = new
                        {
                            issueDate= lastIssuedDate,
                            Movie = movie,
                            Writer = writer,
                            Episode = lastIssuedMovie.Episode,

                          };


                        string resultJson = JsonConvert.SerializeObject(result, jsonSettings);

                        HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.OK);
                        response.Content = new StringContent(resultJson, Encoding.UTF8, "application/json");

                        return response;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred: " + ex.Message);
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, "An error occurred while processing the request.");
                }
            }
        }


        [HttpPut]
        public HttpResponseMessage UpdateSubscription(int Reader_ID, string subscription)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();

            var reader = db.Reader.FirstOrDefault(r => r.Reader_ID == Reader_ID);

            if (reader != null)
            {
                reader.Subscription = subscription;
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, "Subscription updated successfully");
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, "User not found");
            }

        }

        [HttpPut]
        public HttpResponseMessage SendBalanceRequest(int Reader_ID, int Amount)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();

            DateTime currentDate = new DateTime();

            var balance = new BalanceRequests
            {
                Balance_ID = GenerateId(),
                Reader_ID = Reader_ID,
                Balance = Amount,
                Status = "Sent",
                RequestDate = currentDate.ToString(),
                adminNotifications = true
            };
            db.BalanceRequests.Add(balance);
            db.SaveChanges();
          

                return Request.CreateResponse(HttpStatusCode.OK, "Subscription updated successfully");
           

        }

       

        [HttpPut]
        public HttpResponseMessage UpdateInterests(int ID, string newInterests,string Role)
        {
           
          /*  string interestsString = string.Join(",", newInterests);*/

            using (BlinkMovieEntities db = new BlinkMovieEntities())
            {
               if(Role == "Reader")
                {
                    var reader = db.Reader.FirstOrDefault(r => r.Reader_ID == ID);

                    reader.Interest = newInterests;
                    db.SaveChanges();

                    return Request.CreateResponse(HttpStatusCode.OK, "Interests updated successfully");
                }

               else if(Role == "Writer")
                {
                    var writer = db.Writer.FirstOrDefault(r => r.Writer_ID == ID);

                    writer.Interest = newInterests;
                    db.SaveChanges();

                    return Request.CreateResponse(HttpStatusCode.OK, "Interests updated successfully");
                }
                
                else
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "User not found");
                }
            }
        }

        [HttpGet]
        public HttpResponseMessage GetAllMovies(string genre=null,string searchTerm = null)
        {
            using (BlinkMovieEntities db = new BlinkMovieEntities())
            {
                db.Configuration.LazyLoadingEnabled = false;

                var moviesQuery = from gm in db.GetMovie
                                  join m in db.Movie on gm.Movie_ID equals m.Movie_ID
                                  where m.Type == "Movie"
                                  select new
                                  {
                                      Movie_ID = m.Movie_ID,
                                      Name = m.Name,
                                      Type = m.Type,
                                      Image = m.Image,
                                      Rating = m.AverageRating,
                                      Category = m.Category
                                  };

               
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    if (moviesQuery.Any(m => m.Name.Contains(searchTerm)))
                    {
                        moviesQuery = moviesQuery.Where(m => m.Name.Contains(searchTerm));
                    }
                    else
                    {
                       
                        moviesQuery = moviesQuery;
                    }
                }
                else if (!string.IsNullOrEmpty(genre))
                {
                    moviesQuery = moviesQuery.Where(m => m.Category.Contains(genre));
                    
                }

                /* else if (!string.IsNullOrEmpty(searchTerm) && !string.IsNullOrEmpty(genre))
                  {
                      moviesQuery = moviesQuery.Where(m => m.Name.Contains(searchTerm) || m.Category.Contains(genre));
                  }*/

             /*   if (!string.IsNullOrEmpty(searchTerm))
                {
                    moviesQuery = moviesQuery.Where(m => m.Name.Contains(searchTerm));
                }
*/
                var movies = moviesQuery.Distinct().ToList();

                if (movies.Any())
                {
                    return Request.CreateResponse(HttpStatusCode.OK, movies);
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Data not found");
                }
            }
        }


        [HttpGet]
        public HttpResponseMessage GetAllDramas(string genre = null,string searchTerm = null)
        {
            using (BlinkMovieEntities db = new BlinkMovieEntities())
            {
                db.Configuration.LazyLoadingEnabled = false;

                var moviesQuery = from gm in db.GetMovie
                                  join m in db.Movie on gm.Movie_ID equals m.Movie_ID
                                  where m.Type == "Drama"
                                  select new
                                  {
                                      Movie_ID = m.Movie_ID,
                                      Name = m.Name,
                                      Type = m.Type,
                                      Image = m.Image,
                                      Category =m.Category,
                                      Rating = m.AverageRating,
                                  };


                if (!string.IsNullOrEmpty(searchTerm))
                {
                    if (moviesQuery.Any(m => m.Name.Contains(searchTerm)))
                    {
                        moviesQuery = moviesQuery.Where(m => m.Name.Contains(searchTerm));
                    }
                    else
                    {

                        moviesQuery = moviesQuery;
                    }
                }
                else if (!string.IsNullOrEmpty(genre))
                {
                    moviesQuery = moviesQuery.Where(m => m.Category.Contains(genre));

                }

                var movies = moviesQuery.Distinct().ToList();

                if (movies.Any())
                {
                    return Request.CreateResponse(HttpStatusCode.OK, movies);
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Data not found");
                }
            }
        }


        [HttpGet]
        public HttpResponseMessage GetAllMovieWriter(int movieId, string searchTerm = null)
        {
            using (BlinkMovieEntities db = new BlinkMovieEntities())
            {
                db.Configuration.LazyLoadingEnabled = false;

                var writersQuery = from gm in db.GetMovie
                                   join w in db.Writer on gm.Writer_ID equals w.Writer_ID
                                   where gm.Movie_ID == movieId
                                   select new
                                   {
                                       Writer_ID = w.Writer_ID,
                                       UserName = w.UserName,
                                       Email = w.Email,
                                       Image = w.Image,
                                       Rating = w.AverageRating
                                   };

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    string lowerCaseSearchTerm = searchTerm.ToLower();
                    writersQuery = writersQuery.Where(w => w.UserName.ToLower().Contains(lowerCaseSearchTerm) );
                }

                var writers = writersQuery.Distinct().ToList();

                if (writers.Any())
                {
                    return Request.CreateResponse(HttpStatusCode.OK, writers);
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Data not found");
                }
            }
        }




        [HttpGet]
        public HttpResponseMessage GetSpecificMovie(int Movie_ID)
        {
            try
            {
                using (var db = new BlinkMovieEntities())
                {
                    db.Configuration.LazyLoadingEnabled = false;

                    var movieData = db.Movie
                        .Where(m => m.Movie_ID == Movie_ID)
                        .Select(m => new
                        {
                            Movie = m,
                            Writers = db.Summary
                                .Where(s => s.Movie_ID == Movie_ID)
                                .Select(s => new
                                {
                                    s.Writer_ID,
                                    WriterUserName = db.Writer
                                        .Where(w => w.Writer_ID == s.Writer_ID)
                                        .Select(w => w.UserName)
                                        .FirstOrDefault()
                                })
                                .ToList()
                        })
                        .FirstOrDefault();

                    if (movieData != null)
                    {
                        var response = new
                        {
                            movies =movieData.Movie,
                            Writers = movieData.Writers,
                            count = movieData.Writers.Count()                       };

                        return Request.CreateResponse(HttpStatusCode.OK, response);
                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.NotFound, "Data not found");
                    }
                }
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, $"An error occurred: {ex.Message}");
            }
        }


        /*  [HttpGet]
          public HttpResponseMessage GetMovieDetails(int Movie_ID)
          {
              using (BlinkMovieEntities db = new BlinkMovieEntities())
              {

                  db.Configuration.LazyLoadingEnabled = false;
                  var movieIds = db.GetMovie.Where(getMovie => getMovie.Movie_ID == Movie_ID).Select(getMovie => getMovie.Movie_ID);

                  var result = db.GetMovie
                      .Where(getMovie => movieIds.Contains(getMovie.Movie_ID))
                      .Join(db.Movie, getMovie => getMovie.Movie_ID, movie => movie.Movie_ID, (getMovie, movie) => new { getMovie, movie })
                      .Join(db.Summary, x => new { x.getMovie.Movie_ID, x.getMovie.Writer_ID }, summary => new { Movie_ID = summary.Movie_ID, Writer_ID = summary.Writer_ID }, (x, summary) => new { x.getMovie, x.movie, summary })
                      .Join(db.Writer, summary => summary.summary.Writer_ID, writer => writer.Writer_ID, (summary, writer) => new { summary.getMovie, summary.movie, summary.summary, writer })
                      .Distinct()
                      .ToList();

                  if (result != null)
                  {

                      var responseData = result.Select(item => new
                      {
                          Movie = new
                          {
                              item.movie.Movie_ID,
                              item.movie.Name

                          },
                          Writer = new
                          {
                              item.writer.Writer_ID,
                              item.writer.UserName
                          }

                      });
                      return Request.CreateResponse(HttpStatusCode.OK, responseData);
                  }
                  else
                  {
                      return Request.CreateResponse(HttpStatusCode.NotFound, "Movie not found");
                  }
              }
          }
  */

        [HttpGet]
        public HttpResponseMessage GetSummary(int Writer_ID, int Movie_ID)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();

            db.Configuration.LazyLoadingEnabled = false;
            var result = db.Summary
                .Where(summary => summary.Writer_ID == Writer_ID && summary.Movie_ID == Movie_ID)
                .Join(db.Writer, summary => summary.Writer_ID, writer => writer.Writer_ID, (summary, writer) => new { summary, writer })
                .Join(db.Movie, x => x.summary.Movie_ID, movie => movie.Movie_ID, (x, movie) => new { x.summary, x.writer, movie })
                .ToList();

            if (result != null && result.Any())
            {
                var responseData = result.Select(item => new
                {
                    Summary = new
                    {
                        item.summary.Summary_ID,
                        item.summary.Summary1
                    },
                    Writer = new
                    {
                        item.writer.Writer_ID,
                        item.writer.UserName
                    },
                    Movie = new
                    {
                        item.movie.Movie_ID,
                    }
                });
                return Request.CreateResponse(HttpStatusCode.OK, responseData);
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, "Movie not found");
            }
        }

        [HttpPut]
        public HttpResponseMessage UpdateReaderPassword(int Reader_ID, string newPassword)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();
            try
            {
                var reader = db.Reader.FirstOrDefault(r => r.Reader_ID == Reader_ID);
                if (reader == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Reader not found");
                }

                
                reader.Password = newPassword;

                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, "Reader password updated successfully");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
        [HttpPut]
        public HttpResponseMessage UpdateReaderName(int Reader_ID, string newName)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();
            try
            {
                var reader = db.Reader.FirstOrDefault(r => r.Reader_ID == Reader_ID);
                if (reader == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Reader not found");
                }

               
                reader.UserName = newName;

                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, "Reader name updated successfully");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
        [HttpGet]
        public HttpResponseMessage SearchMovies(string searchQuery)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();
            try
            {
                
                var searchResults = db.Movie
                    .Where(m => m.Name.Contains(searchQuery) || m.Category.Contains(searchQuery))
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, searchResults);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetClip(int Writer_ID, int Movie_ID)
        {
            BlinkMovieEntities db = new BlinkMovieEntities();

            db.Configuration.LazyLoadingEnabled = false;
            var result = db.Clips
                .Where(clip => clip.Writer_ID == Writer_ID && clip.Movie_ID == Movie_ID)
                .Join(db.Writer, clip => clip.Writer_ID, writer => writer.Writer_ID, (clip, writer) => new { clip, writer })
                .Join(db.Movie, x => x.clip.Movie_ID, movie => movie.Movie_ID, (x, movie) => new { x.clip, x.writer, movie })
                .ToList();

            if (result != null && result.Any())
            {
                var responseData = result.Select(item => new
                {
                    Clips = new
                    {
                        item.clip.Clips_ID,
                        item.clip.Url,
                        item.clip.Start_time,
                        item.clip.End_time
                    },
                    Writer = new
                    {
                        item.writer.Writer_ID,
                    },
                    Movie = new
                    {
                        item.movie.Movie_ID,
                    },
                });
                return Request.CreateResponse(HttpStatusCode.OK, responseData);
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, "Clip not found");
            }
        }
        [HttpGet]
        public HttpResponseMessage GetFavoriteDetails(int Reader_ID)
        {
            try
            {
                using (var db = new BlinkMovieEntities())
                {
                    var result = db.Favorites
                        .Where(fav => fav.Reader_ID == Reader_ID)
                        .Join(db.Writer, fav => fav.Writer_ID, writer => writer.Writer_ID, (fav, writer) => new { fav, writer })
                        .Join(db.Movie, x => x.fav.Movie_ID, movie => movie.Movie_ID, (x, movie) => new { x.fav, x.writer, movie })
                        .ToList();

                    var details = result.Select(x => new
                    {
                        ReaderId = x.fav.Reader_ID,
                        WriterId = x.fav.Writer_ID,
                        MovieId = x.fav.Movie_ID,
                        WriterName = x.writer.UserName,
                        MovieTitle = x.movie.Name,
                        Director = x.movie.Director,
                        MovieRating = x.movie.Rating,
                        WriterRating = x.writer.Rating

                    }).ToList<object>();

                    return Request.CreateResponse(HttpStatusCode.OK, details);
                }
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetHistoryDetails(int Reader_ID)
        {
            try
            {
                using (var db = new BlinkMovieEntities())
                {
                    var result = db.History
                        .Where(history => history.Reader_ID == Reader_ID)
                        .Join(db.Writer, history => history.Writer_ID, writer => writer.Writer_ID, (history, writer) => new { history, writer })
                        .Join(db.Movie, x => x.history.Movie_ID, movie => movie.Movie_ID, (x, movie) => new { x.history, x.writer, movie })
                        .ToList();

                    var details = result.Select(x => new
                    {
                        ReaderId = x.history.Reader_ID,
                        WriterId = x.history.Writer_ID,
                        MovieId = x.history.Movie_ID,
                        WriterName = x.writer.UserName,
                        MovieTitle = x.movie.Name,
                        Director = x.movie.Director,
                        MovieRating = x.movie.Rating,
                        WriterRating = x.writer.Rating
                    }).ToList<object>();

                    return Request.CreateResponse(HttpStatusCode.OK, details);
                }
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]

        public HttpResponseMessage GetTopRatedMovies()
        {
            BlinkMovieEntities db = new BlinkMovieEntities();
            db.Configuration.LazyLoadingEnabled = false;

            var movies = db.Movie.Where(m => m.AverageRating > 4.0 && m.Type=="Movie").Select( s => new
            {
                s.Movie_ID,
                s.Rating,
                s.Name,
                s.AverageRating,
                s.Image,
                s.Type,
                s.Category,
                s.Director
            }).ToList();

            if (movies != null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, movies);
            }

            else
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, "Data not found");
            }

        }

        [HttpGet]

        public HttpResponseMessage GetTopRatedDrama()
        {
            BlinkMovieEntities db = new BlinkMovieEntities();
            db.Configuration.LazyLoadingEnabled = false;

            var movies = db.Movie.Where(m => m.AverageRating > 4.0 && m.Type == "Drama").Select(s => new
            {
                s.Movie_ID,
                s.Rating,
                s.Name,
                s.AverageRating,
                s.Image,
                s.Type,
                s.Category,
                s.Director
            }).ToList();

            if (movies != null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, movies);
            }

            else
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, "Data not found");
            }

        }

        [HttpGet]
        public HttpResponseMessage GetMoviesByInterests(int Reader_ID)
        {
            using (BlinkMovieEntities db = new BlinkMovieEntities())
            {
                db.Configuration.LazyLoadingEnabled = false;

                
                var reader = db.Reader.FirstOrDefault(r => r.Reader_ID == Reader_ID);

                if (reader == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Reader not found");
                }

                
                string[] readerInterests = reader.Interest.Split(',');

                
                var movies = db.Movie
                    .Where(m => m.Category != null)
                    .ToList()
                    .Where(m => m.Category.Split(',').Any(g => readerInterests.Contains(g)))
                    .ToList();

                if (movies.Count > 0)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, movies);
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No movies found based on reader's interests");
                }
            }
        }


        [HttpPost]
        public HttpResponseMessage UpdateWriterRating(int Reader_ID,int writerId, double rating)
        {

            using (var db = new BlinkMovieEntities())
            {
                db.Configuration.LazyLoadingEnabled = false;
                db.Configuration.ProxyCreationEnabled = false;
                var  writr = db.Writer.FirstOrDefault(w => w.Writer_ID == writerId);

                if (writr == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Movie not found");
                }


                if (rating < 0 || rating > 5)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Rating should be between 0 and 5");
                }

             var readerRating = db.ReaderRate.Where(rr => rr.Writer_ID == writerId && rr.Reader_ID == Reader_ID).FirstOrDefault();
                var writer = db.Writer.Where(s => s.Writer_ID == writerId).FirstOrDefault();
                int? totalRatings = writr.TotalRatings + 1;
                double? totalRatingSum = writr.TotalRatingSum + rating;
                double? averageRating = (double)totalRatingSum / totalRatings;

                if (readerRating != null)
                {
                    
                    writer.TotalRatings = writer.TotalRatings - 1;
                    writer.TotalRatingSum = writer.TotalRatingSum - readerRating.Movie_Rating;

                    db.SaveChanges();

                  

                    writr.TotalRatings = totalRatings;
                    writr.TotalRatingSum = totalRatingSum;
                    writr.AverageRating = averageRating;


                    db.ReaderRate.Remove(readerRating);
                    db.SaveChanges();

                    var readerRate = new ReaderRate
                    {
                        ReaderRate_ID = GenerateId(),
                        Writer_ID = writerId,
                        Reader_ID = Reader_ID,
                        Writer_Rating = rating
                    };


                    db.ReaderRate.Add(readerRate);
                    db.SaveChanges();
                }

                else
                {

                    writr.TotalRatings = totalRatings;
                    writr.TotalRatingSum = totalRatingSum;
                    writr.AverageRating = averageRating;

                    var readerRate = new ReaderRate
                    {
                        ReaderRate_ID = GenerateId(),
                        Writer_ID = writerId,
                        Reader_ID = Reader_ID,
                        Writer_Rating = rating
                    };


                    db.ReaderRate.Add(readerRate);
                    db.SaveChanges();
                }

               if(writr.TotalRatings >10 && writer.AverageRating > 4.5)
                {
                    var addToEditor = new Editor
                    {
                        Editor_ID =GenerateId(),
                        UserName = writr.UserName,
                        Email = writr.Email,
                        Password =writr.Password
                    };

                    db.Editor.Add(addToEditor);
                    db.SaveChanges();

                    var changeRole = db.Users.Where(u => u.Writer_ID == writerId).FirstOrDefault();
                    changeRole.Editor_ID = addToEditor.Editor_ID;
                    changeRole.Writer_ID = null;
                    changeRole.Role = "Editor";

                    db.SaveChanges();
                }

                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, "Movie rating updated successfully");

            }
        }

        [HttpPost]
        public HttpResponseMessage UpdateMovieRating(int Reader_ID,int movieId, double rating)
        {

            using (var db = new BlinkMovieEntities())
            {
                db.Configuration.LazyLoadingEnabled = false;
                db.Configuration.ProxyCreationEnabled = false;
                var movie = db.Movie.FirstOrDefault(w => w.Movie_ID == movieId);

                if (movie == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Movie not found");
                }


                if (rating < 0 || rating > 5)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Rating should be between 0 and 5");
                }

               /* int? totalRatings = movie.TotalRatings + 1;
                double? totalRatingSum = movie.TotalRatingSum + rating;
                double? averageRating = (double)totalRatingSum / totalRatings;
*/
                var readerRating = db.ReaderRate.Where(rr => rr.Movie_ID == movieId && rr.Reader_ID == Reader_ID).FirstOrDefault();


                if (readerRating != null)
                {
                    var summary= db.Movie.Where(s => s.Movie_ID == movieId).FirstOrDefault();
                    summary.TotalRatings = summary.TotalRatings - 1;
                    summary.TotalRatingSum = summary.TotalRatingSum - readerRating.Movie_Rating;

                    db.SaveChanges();

                    int? totalRatings = movie.TotalRatings + 1;
                    double? totalRatingSum = movie.TotalRatingSum + rating;
                    double? averageRating = (double)totalRatingSum / totalRatings;

                    movie.TotalRatings = totalRatings;
                    movie.TotalRatingSum = totalRatingSum;
                    movie.AverageRating = averageRating;


                    db.ReaderRate.Remove(readerRating);
                    db.SaveChanges();

                    var readerRate = new ReaderRate
                    {
                        ReaderRate_ID = GenerateId(),
                        Movie_ID = movieId,
                        Reader_ID = Reader_ID,
                        Movie_Rating = rating
                    };


                    db.ReaderRate.Add(readerRate);
                    db.SaveChanges();
                }

                    else
                    {
                        int? totalRatings = movie.TotalRatings + 1;
                        double? totalRatingSum = movie.TotalRatingSum + rating;
                        double? averageRating = (double)totalRatingSum / totalRatings;

                        movie.TotalRatings = totalRatings;
                        movie.TotalRatingSum = totalRatingSum;
                        movie.AverageRating = averageRating;

                    var readerRate = new ReaderRate
                    {
                        ReaderRate_ID = GenerateId(),
                        Movie_ID =movieId, 
                        Reader_ID = Reader_ID, 
                        Movie_Rating = rating 
                    };

                   
                    db.ReaderRate.Add(readerRate);
                    db.SaveChanges();
                }

                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, "Movie rating updated successfully");
            }
        }
    }
}


//Free Movie

//in Last
//Balance
//GetNotified
