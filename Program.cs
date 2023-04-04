using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var fonts = new FontCollection();
var textile = new Font(fonts.Add("Textile.ttf"), 30);

var url = app.Configuration["STORAGE_URL"];
var key = app.Configuration["STORAGE_KEY"];
var secret = app.Configuration["STORAGE_SECRET"];

var s3 = new AmazonS3Client(
  awsAccessKeyId: key,
  awsSecretAccessKey: secret,
  clientConfig: new AmazonS3Config
  {
    ForcePathStyle = false,
    RegionEndpoint = RegionEndpoint.USEast1,
    ServiceURL = url
  }
);

var buckets = await s3.ListBucketsAsync();

if (buckets.Buckets.Count == 0)
{
  Console.WriteLine("No buckets found");

  try
  {
    var response = await s3.PutBucketAsync(new PutBucketRequest
    {
      BucketName = "sunny",
      UseClientRegion = true,
    });
  }
  catch (AmazonS3Exception ex)
  {
    Console.WriteLine($"Unable to create bucket :( {ex.Message}");
  }
}
else
{
  Console.WriteLine("Buckets found!");
  buckets.Buckets.ForEach(x => Console.WriteLine($"Name: {x.BucketName}"));
}

MemoryStream MakeImage(string text)
{
  using var img = new Image<Rgba32>(width: 750, height: 500);
  img.Mutate(x => x.Fill(Color.Black));

  var shapes = TextBuilder.GenerateGlyphs(
    text: text,
    textOptions: new TextOptions(textile)
    {
      Origin = new PointF(img.Size.Width / 2F, img.Size.Height / 2F),
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center,
      WrappingLength = 470,
      TextAlignment = TextAlignment.Center
    }
  );

  img.Mutate(x => x.Fill(Color.White, shapes));

  var stream = new MemoryStream();

  img.SaveAsPng(stream);

  stream.Seek(0, SeekOrigin.Begin);

  return stream;
}

app.MapPost("/sunny", async (SunnyRequest request) =>
{
  var text = request.text;
  var stream = MakeImage(text);

  if (request.upload)
  {
    var fileName = $"{System.IO.Path.GetRandomFileName().Split('.')[0]}.png";
    var uploadStream = new MemoryStream();

    await stream.CopyToAsync(uploadStream);

    stream.Seek(0, SeekOrigin.Begin);

    var response = await s3.PutObjectAsync(request: new()
    {
      BucketName = "sunnybot",
      Key = fileName,
      InputStream = uploadStream,
      ContentType = "image/png",
      CannedACL = S3CannedACL.PublicRead
    });

    if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
    {
      Console.WriteLine("Uploaded file!");
    }
  }

  return Results.File(stream, "image/png");
});

app.Run();
