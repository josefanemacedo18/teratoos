using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using TeraTools.Core.Imaging;
using TeraTools.Core.Serialization;

namespace TeraTools.Revit.Infrastructure;

/// <summary>
/// Referências visuais guardadas DENTRO do .rvt: cada imagem (JPEG reduzido) fica num armazenamento
/// próprio, com título, notas, etiquetas e, se quiser, o sistema gerado a que se refere.
/// </summary>
public static class ReferenceStore
{
    private static readonly Guid SchemaGuid = new("F4C901F4-7830-4A9F-99A6-E57C108F0964");
    private const string StorageName = "TeraTools.Referencia";
    public const int MaxSide = 1600;
    public const int Quality = 85;

    public sealed class Item
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        public string Title { get; set; } = "";
        public string Note { get; set; } = "";
        public string Tags { get; set; } = "";
        public string? SystemId { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public int Width { get; set; }
        public int Height { get; set; }

        [System.Text.Json.Serialization.JsonIgnore] public byte[] Jpeg { get; set; } = Array.Empty<byte>();
        [System.Text.Json.Serialization.JsonIgnore] public long StorageId { get; set; } = -1;
        [System.Text.Json.Serialization.JsonIgnore] public bool Dirty { get; set; }

        public Item CloneMeta() => (Item)MemberwiseClone();
    }

    private static Schema GetSchema()
    {
        var s = Schema.Lookup(SchemaGuid);
        if (s != null) return s;
        var b = new SchemaBuilder(SchemaGuid);
        b.SetSchemaName("TeraToolsReference");
        b.SetVendorId("TERA");
        b.SetReadAccessLevel(AccessLevel.Public);
        b.SetWriteAccessLevel(AccessLevel.Public);
        b.SetDocumentation("Imagem de referência do Téra Tools (JPEG em base64 + dados em JSON).");
        b.AddSimpleField("Json", typeof(string));
        b.AddSimpleField("Image", typeof(string));
        return b.Finish();
    }

    public static List<Item> LoadAll(Document doc)
    {
        var list = new List<Item>();
        var schema = GetSchema();
        foreach (var ds in new FilteredElementCollector(doc).OfClass(typeof(DataStorage)).Cast<DataStorage>())
        {
            try
            {
                var e = ds.GetEntity(schema);
                if (e == null || !e.IsValid()) continue;
                var item = Json.Deserialize<Item>(e.Get<string>("Json")) ?? new Item();
                item.Jpeg = Convert.FromBase64String(e.Get<string>("Image") ?? "");
                item.StorageId = ds.Id.Value;
                list.Add(item);
            }
            catch (Exception ex)
            {
                Log.Warn("Referência visual ilegível: " + ex.Message);
            }
        }
        return list.OrderBy(i => i.Date).ToList();
    }

    /// <summary>Cria ou atualiza (precisa de transação aberta).</summary>
    public static void Save(Document doc, Item item)
    {
        DataStorage? ds = item.StorageId >= 0 ? doc.GetElement(new ElementId(item.StorageId)) as DataStorage : null;
        if (ds == null)
        {
            ds = DataStorage.Create(doc);
            ds.Name = StorageName;
            item.StorageId = ds.Id.Value;
        }
        var e = new Entity(GetSchema());
        e.Set("Json", Json.Serialize(item, typeof(Item), indented: false));
        e.Set("Image", Convert.ToBase64String(item.Jpeg));
        ds.SetEntity(e);
        item.Dirty = false;
    }

    public static void Delete(Document doc, Item item)
    {
        if (item.StorageId < 0) return;
        var id = new ElementId(item.StorageId);
        if (doc.GetElement(id) != null) doc.Delete(id);
    }

    // ------------------------------------------------------------------ imagens

    /// <summary>Lê um arquivo de imagem e devolve o JPEG reduzido (lado maior até <see cref="MaxSide"/>).</summary>
    public static (byte[] Jpeg, int Width, int Height) FromFile(string path)
    {
        using var fs = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return Encode(decoder.Frames[0]);
    }

    public static (byte[] Jpeg, int Width, int Height) Encode(BitmapSource source)
    {
        var (w, h) = ImageFit.Fit(source.PixelWidth, source.PixelHeight, MaxSide);
        BitmapSource img = source;
        if (w != source.PixelWidth)
            img = new TransformedBitmap(source, new ScaleTransform((double)w / source.PixelWidth, (double)h / source.PixelHeight));
        // JPEG não tem transparência: achata sobre branco
        if (img.Format != PixelFormats.Bgr24 && img.Format != PixelFormats.Bgr32)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new System.Windows.Rect(0, 0, w, h));
                dc.DrawImage(img, new System.Windows.Rect(0, 0, w, h));
            }
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            img = new FormatConvertedBitmap(rtb, PixelFormats.Bgr24, null, 0);
        }
        var enc = new JpegBitmapEncoder { QualityLevel = Quality };
        enc.Frames.Add(BitmapFrame.Create(img));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return (ms.ToArray(), w, h);
    }

    public static BitmapImage? Decode(byte[] jpeg, int decodeWidth = 0)
    {
        if (jpeg.Length == 0) return null;
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            if (decodeWidth > 0) bi.DecodePixelWidth = decodeWidth;
            bi.StreamSource = new MemoryStream(jpeg);
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch (Exception ex)
        {
            Log.Warn("Decodificar referência: " + ex.Message);
            return null;
        }
    }
}
