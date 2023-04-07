using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;

HttpClient client = new HttpClient();

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles();

var fonts = new FontCollection();
var textile = new Font(fonts.Add("Textile.ttf"), 30);

var url = app.Configuration["STORAGE_URL"];
var key = app.Configuration["STORAGE_KEY"];
var secret = app.Configuration["STORAGE_SECRET"];

var clientId = app.Configuration["SLACK_CLIENT_ID"];
var slackSecret = app.Configuration["SLACK_CLIENT_SECRET"];
var slackSigning = app.Configuration["SLACK_SIGNING_SECRET"];

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

async Task<MemoryStream> MakeImage(string text)
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

  await img.SaveAsPngAsync(stream);

  stream.Seek(0, SeekOrigin.Begin);

  return stream;
}

async Task SendResponse(string responseUrl, string imageUrl, string channel)
{
  var response = new SlackResponse
  {
    response_type = "in_channel",
    channel = channel,
    text = "Your episode card",
    blocks = new() {
      new() {
        image_url = imageUrl,
        alt_text = "sunny card"
      }
    }
  };

  var r = await client.PostAsJsonAsync(responseUrl, response);

  Console.WriteLine(await r.Content.ReadAsStringAsync());
}

app.MapPost("/sunny", async (SunnyRequest request) =>
{
  var text = request.text;
  var stream = await MakeImage(text);

  return Results.File(stream, "image/png");
});

app.MapPost("/slack", async (HttpRequest r) =>
{
  var request = new SlackRequest
  {
    command = r.Form["command"],
    text = r.Form["text"],
    response_url = r.Form["response_url"],
    channel_id = r.Form["channel_id"]
  };

  if (string.IsNullOrWhiteSpace(request.text))
  {
    return Results.Ok("You must supply text");
  }

  var text = request.text;
  var stream = await MakeImage(text);
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

  if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
  {
    return Results.Ok(new
    {
      response_type = "ephemeral",
      text = "Sorry, failed to generate a title card."
    });
  }

  var imgUrl = $"{url}/sunnybot/{fileName}";

  await SendResponse(request.response_url, imgUrl, request.channel_id);

  return Results.Ok();
});

async Task Authorize(string code)
{
  var content = new Dictionary<string, string>
  {
    { "code", code },
    { "client_id", clientId },
    { "client_secret", slackSecret }
  };

  var resp = await client.PostAsync("https://slack.com/api/oauth.v2.access", new FormUrlEncodedContent(content));

  Console.WriteLine(await resp.Content.ReadAsStringAsync());
}

app.MapGet("/oauth", async (string code) =>
{
  await Authorize(code);

  return Results.Ok();
});

app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run();
