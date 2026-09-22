using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Resources;
using PTBox.Launcher.Models;

namespace PTBox.Launcher.Services;

public static class ArtworkService
{
    private static readonly Lazy<BitmapImage?> Landscape = new(() => Load("home-landscape.png"));
    private static readonly Lazy<BitmapImage?> Atlas = new(() => Load("app-scenes.png"));
    public static ImageSource? Hero => Landscape.Value;
    private static BitmapImage? Load(string name)
    {
        try
        {
            // Load from the assembly directly, including before Application exists in tests.
            var resources = new ResourceManager("PTBox.Launcher.g", typeof(ArtworkService).Assembly);
            using var stream = resources.GetStream("assets/artwork/" + name);
            if (stream == null) return null;
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream; image.EndInit();
            image.Freeze(); return image;
        }
        catch { return null; }
    }
    public static string Category(LauncherItem item) => item.Category != "auto" ? item.Category : item.Id switch
    {
        "playnite" or "steam" => "games", "moonlight" => "streaming", "bilibili" or "video" => "media", _ => "apps"
    };
    public static Brush Scene(string id, string category = "apps")
    {
        var index = id switch
        {
            "playnite" => 0, "moonlight" => 1, "bilibili" => 2, "steam" => 3,
            "video" => 4, "edge" => 5, "files" => 6, "$quick-video" => 7, "$desktop" => 8,
            _ => category switch { "games" => 0, "streaming" => 1, "media" => 4, _ => 8 }
        };
        if (Atlas.Value == null) return new SolidColorBrush(Color.FromRgb(28, 49, 68));
        var brush = new ImageBrush(Atlas.Value)
        {
            ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewbox = new Rect(index % 3 / 3.0, index / 3 / 3.0, 1.0 / 3, 1.0 / 3),
            Stretch = Stretch.UniformToFill
        };
        brush.Freeze(); return brush;
    }
    public static ImageSource? Icon(string id)
    {
        // Vector marks remain crisp at 4K; custom user icons always take precedence.
        var drawings = new DrawingGroup();
        var white = Brushes.White;
        void Fill(string path, Brush brush) => drawings.Children.Add(new GeometryDrawing(brush, null, Geometry.Parse(path)));
        void Line(string path, double width = 4) => drawings.Children.Add(new GeometryDrawing(null, new Pen(white, width) { StartLineCap=PenLineCap.Round, EndLineCap=PenLineCap.Round, LineJoin=PenLineJoin.Round }, Geometry.Parse(path)));
        void Circle(double x, double y, double radius, bool fill = true, double stroke = 3) => drawings.Children.Add(new GeometryDrawing(fill ? white : null, fill ? null : new Pen(white, stroke), new EllipseGeometry(new Point(x,y),radius,radius)));
        switch (id)
        {
            case "playnite":
                Fill("M16,20 C7,20 3,37 3,46 C3,54 10,56 16,49 L22,42 L42,42 L48,49 C54,56 61,54 61,46 C61,37 57,20 48,20 Z", white);
                Fill("M16,26 L20,26 L20,31 L25,31 L25,35 L20,35 L20,40 L16,40 L16,35 L11,35 L11,31 L16,31 Z", new SolidColorBrush(Color.FromRgb(45,55,68)));
                drawings.Children.Add(new GeometryDrawing(Brushes.SlateGray,null,new EllipseGeometry(new Point(45,29),3,3)));
                drawings.Children.Add(new GeometryDrawing(Brushes.SlateGray,null,new EllipseGeometry(new Point(51,36),3,3))); break;
            case "moonlight":
                Fill("M43,4 C21,1 6,18 10,37 C15,61 44,67 60,48 C37,56 18,28 43,4 Z",white); break;
            case "bilibili":
                Line("M13,12 L24,21 M49,12 L39,21 M14,22 L50,22 Q57,22 57,30 L57,49 Q57,55 50,55 L14,55 Q7,55 7,49 L7,30 Q7,22 14,22 Z",5);
                Line("M20,34 L20,40 M43,34 L43,40 M27,44 Q32,49 37,44",3); break;
            case "steam":
                Circle(44,20,15,false,5); Circle(44,20,8,false,3); Circle(20,46,9,false,4);
                Line("M3,37 L19,45 M27,45 L42,34 M22,37 L31,23",6); break;
            case "edge":
                Fill("M4,40 C-1,18 13,2 33,2 C55,2 67,24 59,43 C58,24 38,15 22,29 C10,33 7,37 4,40 Z", new LinearGradientBrush(Color.FromRgb(26,210,201),Color.FromRgb(30,98,236),90));
                Fill("M61,38 C55,60 25,68 10,51 C-2,35 10,18 27,22 C9,38 34,60 61,38 Z",new LinearGradientBrush(Color.FromRgb(18,87,208),Color.FromRgb(33,186,221),0));
                Fill("M27,22 C38,13 62,22 61,38 C55,51 32,50 26,40 C46,46 52,23 27,22 Z",new LinearGradientBrush(Color.FromRgb(89,219,140),Color.FromRgb(19,175,179),90)); break;
            case "files":
                Fill("M4,17 Q4,11 10,11 L26,11 L32,18 L56,18 Q61,18 61,24 L61,51 Q61,57 55,57 L9,57 Q4,57 4,51 Z",new SolidColorBrush(Color.FromRgb(255,194,57)));
                Fill("M4,23 L61,23 L61,29 L4,29 Z",new SolidColorBrush(Color.FromRgb(255,224,125))); break;
            default: return null;
        }
        // A transparent bounding box gives all marks a consistent 64×64 viewport.
        drawings.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,64,64))));
        drawings.Freeze(); return new DrawingImage(drawings);
    }
}
