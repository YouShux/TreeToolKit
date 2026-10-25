using Dalamud;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using OmenTools.OmenService;

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

    private static readonly Dictionary<float, IFontHandle> UIHandles = [];
    private static IReadOnlyList<KeyValuePair<string, string>> InstalledFonts = [];
    private static object? InstalledFontsSource;
    private static string CurrentPath = string.Empty;
    private static float CurrentSize;
    private static IUiBuilder? UIBuilder;

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
            UIBuilder = null;
            InstalledFonts = [];
            InstalledFontsSource = null;
            CurrentPath = string.Empty;
            CurrentSize = 0;
        }
    }

    public static bool TryGetGameFamily(string path, out GameFontFamily family)
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

        InstalledFonts = result;
        InstalledFontsSource = manager.InstalledFontFamilies;
        return InstalledFonts;
    }

    public static IFontHandle GetUIFont(float scale = 1f)
    {
        var manager = FontManager.Instance();
        var path = manager.Config.FontFileName;
        var baseSize = manager.GetActualFontSize(1f);
        if (CurrentPath != path || CurrentSize != baseSize)
        {
            Dispose();
            CurrentPath = path;
            CurrentSize = baseSize;
        }
        if (!TryGetGameFamily(path, out var family))
        {
            return manager.GetUIFont(scale);
        }
        var size = manager.GetActualFontSize(scale);
        if (!UIHandles.TryGetValue(size, out var font))
        {
            font = CreateGameFont(family, size);
            UIHandles.Add(size, font);
        }
        return font.Available ? font : manager.GetUIFont(scale);
    }

    public static IFontHandle CreateGameFont(GameFontFamily family, float size) =>
        (UIBuilder ?? throw new InvalidOperationException("TreeToolKit 运行上下文尚未初始化。"))
        .FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            var font = tk.AddGameGlyphs(new GameFontStyle(family, size), null, default);
            tk.AddGameSymbol(new()
            {
                SizePx = size, MergeFont = font
            });
            tk.AddFontAwesomeIconFont(new()
            {
                SizePx = size, MergeFont = font
            });
            // 游戏字体缺少的中文及其他字符由随 Dalamud 分发的字体补齐。
            tk.AddDalamudAssetFont(DalamudAsset.NotoSansCjkRegular,
                new()
                {
                    SizePx = size, MergeFont = font, FontNo = 2, GlyphRanges = [0x20, 0xFFEF, 0]
                });
            tk.Font = font;
        }));

    public static void Dispose()
    {
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
