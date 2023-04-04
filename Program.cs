using Microsoft.AspNetCore.Mvc;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var fonts = new FontCollection();
var textile = new Font(fonts.Add("Textile.ttf"), 30);

app.MapPost("/sunny", (SunnyRequest request) =>
{
  var text = request.text;

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

  return Results.File(stream, "image/png");
});

app.Run();
