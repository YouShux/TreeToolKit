using Dalamud;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using OmenTools.OmenService;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace TreeToolKit;

public static class Fonts
{
    public static IReadOnlyList<KeyValuePair<string, string>> GameFonts { get; } =
    [
        new("game:Axis", "Axis"),
        new("game:MiedingerMid", "Miedinger Medium"),
        new("game:Meidinger", "Miedinger"),
        new("game:TrumpGothic", "Trump Gothic")
    ];

    private static readonly Dictionary<float, IFontHandle>     UIHandles            = [];
    private static IReadOnlyList<KeyValuePair<string, string>> InstalledFonts       = [];
    private static object?                                     InstalledFontsSource;
    private static string                                      CurrentPath          = string.Empty;
    private static float                                       CurrentSize;
    private static int                                         CurrentFaceIndex;
    private static IUiBuilder?                                 UIBuilder;
    private static readonly ConditionalWeakTable<IFontHandle, object> ObservedFonts = new();
    private static readonly object ObservedFontsLock = new();
    private static long RevisionValue;

    public static long Revision => Interlocked.Read(ref RevisionValue);

    public static IFontHandle Observe(IFontHandle font)
    {
        if (ObservedFonts.TryGetValue(font, out _))
            return font;
        lock (ObservedFontsLock)
        {
            if (!ObservedFonts.TryGetValue(font, out _))
            {
                font.ImFontChanged += OnFontChanged;
                ObservedFonts.Add(font, new());
            }
        }
        return font;
    }

    private static void OnFontChanged(IFontHandle font, ILockedImFont lockedFont) =>
        Interlocked.Increment(ref RevisionValue);

    internal static void Initialize(IUiBuilder uiBuilder)
    {
        ArgumentNullException.ThrowIfNull(uiBuilder);
        if (UIBuilder is not null)
            throw new InvalidOperationException("TreeToolKit 字体资源已由另一个运行实例持有。");
        UIBuilder = uiBuilder;
    }

    internal static void Shutdown()
    {
        try
        {
            Dispose();
        }
        finally
        {
            lock (ObservedFontsLock)
            {
                foreach (var observed in ObservedFonts)
                    observed.Key.ImFontChanged -= OnFontChanged;
                ObservedFonts.Clear();
            }
            Interlocked.Increment(ref RevisionValue);
            UIBuilder            = null;
            InstalledFonts       = [];
            InstalledFontsSource = null;
            CurrentPath          = string.Empty;
            CurrentSize          = 0;
            CurrentFaceIndex     = 0;
        }
    }

    public static bool TryGetGameFamily
    (
        string             path,
        out GameFontFamily family
    )
    {
        family = path switch
        {
            "game:Axis" => GameFontFamily.Axis,
            "game:MiedingerMid" => GameFontFamily.MiedingerMid,
            "game:Meidinger" => GameFontFamily.Meidinger,
            "game:TrumpGothic" => GameFontFamily.TrumpGothic,
            _ => GameFontFamily.Undefined
        };
        return family != GameFontFamily.Undefined;
    }

    public static IReadOnlyList<KeyValuePair<string, string>> GetInstalledFonts()
    {
        var manager = FontManager.Instance();
        if (ReferenceEquals(InstalledFontsSource, manager.InstalledFontFamilies))
        {
            return InstalledFonts;
        }

        var result = new List<KeyValuePair<string, string>>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in manager.InstalledFontFamilies)
        {
            foreach (var face in family.Value)
            {
                if (string.IsNullOrWhiteSpace(face.FilePath) || !seenPaths.Add(face.FilePath))
                {
                    continue;
                }

                var familyName = string.IsNullOrWhiteSpace(face.FamilyName) ? family.Key : face.FamilyName;
                var label = string.IsNullOrWhiteSpace(face.SubfamilyName) ||
                            string.Equals(face.SubfamilyName, "Regular", StringComparison.OrdinalIgnoreCase)
                    ? familyName
                    : $"{familyName} {face.SubfamilyName}";
                result.Add(new(face.FilePath, label));
            }
        }

        InstalledFonts       = result;
        InstalledFontsSource = manager.InstalledFontFamilies;
        return InstalledFonts;
    }

    public static IFontHandle GetUIFont(float scale = 1f)
    {
        var manager = FontManager.Instance();
        var path = manager.Config.FontFileName;
        var baseSize = manager.GetActualFontSize(1f);
        var faceIndex = manager.Config.FontFaceIndex;
        if (CurrentPath != path || CurrentSize != baseSize || CurrentFaceIndex != faceIndex)
        {
            Dispose();
            CurrentPath = path;
            CurrentSize = baseSize;
            CurrentFaceIndex = faceIndex;
        }
        var isGameFont = TryGetGameFamily(path, out var family);
        var size = manager.GetActualFontSize(scale);
        if (!UIHandles.TryGetValue(size, out var font))
        {
            font = isGameFont ? CreateGameFont(family, size) : CreateFileFont(path, faceIndex, size);
            UIHandles.Add(size, font);
        }
        return Observe(font.Available ? font : manager.GetUIFont(scale));
    }

    public static IFontHandle CreateGameFont
    (
        GameFontFamily family,
        float          size
    ) => CreateFont(size, tk => tk.AddGameGlyphs(new GameFontStyle(family, size), null, default),
        2, [0x20, 0xFFEF, 0]);

    private static IFontHandle CreateFileFont
    (
        string path,
        int    faceIndex,
        float  size
    ) => CreateFont(size, tk => File.Exists(path)
        ? tk.AddFontFromFile(path, new()
          {
              SizePx = size, PixelSnapH = true, FontNo = faceIndex, GlyphRanges = [0x20, 0xFFEF, 0]
          })
        : tk.AddDalamudDefaultFont(size, [0x20, 0xFFEF, 0]),
        1, [0x1100, 0x11FF, 0x3130, 0x318F, 0xA960, 0xA97F, 0xAC00, 0xD7FF, 0]);

    private static IFontHandle CreateFont
    (
        float                                             size,
        Func<IFontAtlasBuildToolkitPreBuild, ImFontPtr>     build,
        int                                               fallbackFaceIndex,
        ushort[]                                          fallbackRanges
    ) =>
        (UIBuilder ?? throw new InvalidOperationException("TreeToolKit 运行上下文尚未初始化。"))
        .FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            var font = build(tk);
            tk.AddGameSymbol(new()
            {
                SizePx = size, MergeFont = font
            });
            tk.AddFontAwesomeIconFont(new()
            {
                SizePx = size, MergeFont = font
            });
            // 游戏字体缺少的中文及其他字符由随 Dalamud 分发的字体补齐。
            // 文件字体补入韩文，保留所选字体的已有字形。
            tk.AddDalamudAssetFont(DalamudAsset.NotoSansCjkRegular,
                new()
                {
                    SizePx = size, MergeFont = font, FontNo = fallbackFaceIndex, GlyphRanges = fallbackRanges
                });
            tk.Font = font;
        }));

    public static void Dispose()
    {
        Interlocked.Increment(ref RevisionValue);
        List<Exception>? errors = null;
        foreach (var font in UIHandles.Values)
        {
            try
            {
                font.Dispose();
            }
            catch (Exception exception)
            {
                (errors ??= []).Add(exception);
            }
        }
        UIHandles.Clear();
        if (errors is not null)
            throw new AggregateException("TreeToolKit 字体资源释放失败。", errors);
    }
}
