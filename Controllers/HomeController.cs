using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Amazon.S3;
using Amazon.S3.Transfer;
using Xabe.FFmpeg;

namespace WebApplication5.Controllers
{
    public class HomeController : Controller
    {
        private readonly string _uploadsDir;
        private readonly IAmazonS3 _s3Client;

        public HomeController(IWebHostEnvironment hostingEnvironment)
        {
            _uploadsDir = Path.Combine(hostingEnvironment.WebRootPath, "uploads");
            Directory.CreateDirectory(_uploadsDir);

            var s3Config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.EUNorth1,
                ServiceURL = "https://s3.eu-north-1.amazonaws.com"
            };

            _s3Client = new AmazonS3Client(s3Config);
        }

        [HttpGet]
        public ActionResult Index() => View();

        
        [HttpPost]
        public async Task<IActionResult> UploadFiles(IFormFile file, int start, int end)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("File not selected");
            }

            
            var filePath = Path.Combine(_uploadsDir, file.FileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            if (start != 0 || end != 0)
            {
                await ConvertVideo(filePath, start, end);
            }

            await UploadToS3(filePath, file.FileName);

            TempData["Message"] = "Submitted successfully!";

            return RedirectToAction("UploadedVideos");
        }

        public async Task ConvertVideo(string inputPath, int start, int end)
        {
            var output = Path.Combine(_uploadsDir, "converted.mp4");

            Xabe.FFmpeg.FFmpeg.SetExecutablesPath(_uploadsDir); 

            var conversion = await Xabe.FFmpeg.FFmpeg.Conversions.FromSnippet.Convert(inputPath, output);
            await conversion.Start();
        }

        public async Task UploadToS3(string filePath, string fileName)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open))
            {
                var uploadRequest = new TransferUtilityUploadRequest
                {
                    BucketName = "jeniawsbucket16",
                    Key = fileName,
                    InputStream = fileStream,
                    ContentType = "video/mp4"
                };

                var fileTransferUtility = new TransferUtility(_s3Client);
                await fileTransferUtility.UploadAsync(uploadRequest);
            }
        }

        public IActionResult UploadedVideos()
        {
            var videos = Directory.GetFiles(_uploadsDir, "*.*").Select(Path.GetFileName).ToList();
            return View(videos);
        }
    }
}
