using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace BnlCommunityFixes.Avalonia;

/// <summary>
/// Applies Simplified Chinese text to every launcher window. Static XAML labels and
/// text supplied by view models are translated after binding, so dialogs opened later
/// receive the same localization without duplicating resources in every view.
/// </summary>
internal static class ChineseLocalization
{
    private static DispatcherTimer? timer;

    private static readonly IReadOnlyDictionary<string, string> Text = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Block N Load Community Fixes V2"] = "Block N Load 社区修复 V2",
        ["Block N Load Community Fixes — Update"] = "Block N Load 社区修复 — 更新",
        ["BNL Community Fixes — Updating"] = "BNL 社区修复 — 正在更新",
        ["Block N Load — Game Setup"] = "Block N Load — 游戏设置",
        ["Server"] = "服务器",
        ["Launch Game"] = "启动游戏",
        ["Settings profile"] = "设置配置",
        ["Personal Settings"] = "个人设置",
        ["Feature Settings"] = "功能设置",
        ["Import / Export..."] = "导入／导出…",
        ["Import / Export Configs"] = "导入／导出配置",
        ["More options..."] = "更多选项…",
        ["Match Replays"] = "比赛录像",
        ["Open Folder"] = "打开文件夹",
        ["Browse Replays"] = "浏览录像",
        ["Record match replays"] = "录制比赛录像",
        ["Custom"] = "自定义",
        ["Casual"] = "休闲",
        ["Ranked"] = "排位",
        ["Close"] = "关闭",
        ["Cancel"] = "取消",
        ["OK"] = "确定",
        ["Yes"] = "是",
        ["No"] = "否",
        ["Save"] = "保存",
        ["Save & Close"] = "保存并关闭",
        ["Enabled"] = "已启用",
        ["Add"] = "添加",
        ["Remove"] = "移除",
        ["Browse…"] = "浏览…",
        ["Import Folder"] = "导入文件夹",
        ["Source"] = "来源",
        ["Replacement"] = "替换文件",
        ["Analyze"] = "分析",
        ["Validation"] = "验证报告",
        ["Open Location"] = "打开位置",
        ["Delete"] = "删除",
        ["Refresh"] = "刷新",
        ["Launch Replay Mode"] = "启动录像模式",
        ["Date"] = "日期",
        ["Map"] = "地图",
        ["Duration"] = "时长",
        ["Winner"] = "获胜方",
        ["Size"] = "大小",
        ["Status"] = "状态",
        ["File"] = "文件",
        ["Installing update…"] = "正在安装更新…",
        ["Preparing…"] = "正在准备…",
        ["Skip for now"] = "暂时跳过",
        ["Install now"] = "立即安装",
        ["Download & Install"] = "下载并安装",
        ["I already have the files"] = "我已有游戏文件",
        ["Verify Files"] = "验证文件",
        ["Open files"] = "打开文件",
        ["Install folder:"] = "安装文件夹：",
        ["Download the game below, or point the launcher to an existing installation."] = "请在下方下载游戏，或选择已有的游戏安装目录。",
        ["Block N Load was not found on this PC."] = "此电脑上未找到 Block N Load。",
        ["Also download audio dependencies (required if you have no sound in-game)"] = "同时下载音频依赖（游戏没有声音时需要）",
        ["Manual download mirrors (use if download fails, then choose 'I already have the files'):"] = "手动下载镜像（下载失败时使用，然后选择“我已有游戏文件”）：",
        ["Launcher settings"] = "启动器设置",
        ["Launcher log"] = "启动器日志",
        ["Patching folder"] = "补丁文件夹",
        ["Custom Servers"] = "自定义服务器",
        ["Manage Servers"] = "管理服务器",
        ["New"] = "新建",
        ["Name"] = "名称",
        ["Host"] = "主机",
        ["Port"] = "端口",
        ["Patch"] = "补丁",
        ["Key"] = "键",
        ["Default"] = "默认",
        ["Game"] = "游戏",
        ["Misc"] = "其他",
        ["Map Render"] = "地图渲染",
        ["Damage/Healing"] = "伤害／治疗",
        ["Crosshair"] = "准星",
        ["Team Colors"] = "队伍颜色",
        ["Low Health"] = "低生命值",
        ["Shield Timer"] = "护盾计时器",
        ["Bot Mode"] = "机器人模式",
        ["Bot count"] = "机器人数量",
        ["Difficulty"] = "难度",
        ["Map key"] = "地图键",
        ["Warning: enabling this bypasses Steam login entirely. The game will not connect to any server while Bot Mode is active."] = "警告：启用后将完全绕过 Steam 登录。机器人模式启用期间，游戏不会连接任何在线服务器。",
        ["How many AI opponents to spawn on the enemy team (1–9)."] = "在敌方队伍中生成的 AI 对手数量（1–9）。",
        ["Leave as 'default' to let the runtime pick the first available Friendly map (falling back to Tutorial). Enter a specific catalogue map key to force a particular map."] = "保留“default”可自动选择首个可用的友好模式地图（否则使用教程地图）。也可输入目录中的地图键来指定地图。",
        ["Select All"] = "全选",
        ["Select None"] = "全部取消",
        ["Select which features to include in the import or export:"] = "选择导入或导出时包含的功能：",
        ["Import…"] = "导入…",
        ["Export…"] = "导出…",
        ["Settings are written into the local patching config files and rebuilt/applied on next launch."] = "设置将写入本地补丁配置文件，并在下次启动时重新构建和应用。",
        ["Basic colors"] = "基本颜色",
        ["Pick Color"] = "选择颜色",
        ["Brightness"] = "亮度",
        ["Alpha"] = "透明度",
        ["Hex"] = "十六进制",
        ["Preset"] = "预设",
        ["Season presets:"] = "赛季预设：",
        ["Classic"] = "经典",
        ["Beta"] = "测试版",
        ["Shape"] = "形状",
        ["Scale"] = "缩放",
        ["Spread"] = "扩散",
        ["Gap"] = "间距",
        ["Line thickness"] = "线条粗细",
        ["Hide crosshair"] = "隐藏准星",
        ["FOV"] = "视野",
        ["Weapon model FOV"] = "武器模型视野",
        ["ADS sensitivity"] = "瞄准灵敏度",
        ["Force show in ADS"] = "瞄准时强制显示",
        ["Display mode"] = "显示模式",
        ["Clock size"] = "时钟大小",
        ["Offset X"] = "X 偏移",
        ["Offset Y"] = "Y 偏移",
        ["Indicator size"] = "指示器大小",
        ["Indicator alpha"] = "指示器透明度",
        ["Show direction"] = "显示方向",
        ["Show direction indicator"] = "显示方向指示器",
        ["Show friendly healing"] = "显示友方治疗",
        ["Show self healing"] = "显示自身治疗",
        ["Combine damage"] = "合并伤害数字",
        ["Combine healing"] = "合并治疗数字",
        ["Damage size"] = "伤害数字大小",
        ["Heal size"] = "治疗数字大小",
        ["Self-heal size"] = "自身治疗数字大小",
        ["Self-heal X offset"] = "自身治疗 X 偏移",
        ["Self-heal Y offset"] = "自身治疗 Y 偏移",
        ["Self-healing numbers"] = "自身治疗数字",
        ["Min heal"] = "最小治疗量",
        ["Threshold (0–1)"] = "阈值（0–1）",
        ["Friendly"] = "友方",
        ["Enemy"] = "敌方",
        ["Teammate HP display"] = "队友生命值显示",
        ["Aim healthbar"] = "瞄准生命条",
        ["Deathcam healthbar"] = "死亡镜头生命条",
        ["Segmented healthbar"] = "分段生命条",
        ["Objective Beam"] = "目标光柱",
        ["Heal Alerts"] = "治疗提示",
        ["Unit GUI Scale"] = "单位界面缩放",
        ["WSI Scale"] = "世界指示器缩放",
        ["Optimize Device Health Bars"] = "优化设备生命条",
        ["Disable auto-crouch"] = "禁用自动蹲伏",
        ["Motion blur (prototype)"] = "运动模糊（原型）",
        ["Strength"] = "强度",
        ["Quality"] = "质量",
        ["Center focus"] = "中心清晰度",
        ["Color Grading / Sharpening"] = "色彩分级／锐化",
        ["Sharpening"] = "锐化",
        ["Saturation"] = "饱和度",
        ["Contrast"] = "对比度",
        ["Brightness"] = "亮度",
        ["Temperature"] = "色温",
        ["Nigel Sniper Material (Prototype)"] = "奈杰尔狙击枪材质（原型）",
        ["Applies a cooler gunmetal tint and enhanced specular response only to Nigel's base one-barrel sniper rifle. The mesh, textures, rig, animations, and weapon behavior remain unchanged. Requires rebuild."] = "仅为奈杰尔的基础单管狙击枪应用更冷的枪钢色调和增强的高光反射。模型、纹理、骨骼、动画和武器行为保持不变。需要重新构建。",
        ["Legacy Weapon Models (Prototype)"] = "旧版武器模型（原型）",
        ["Nigel weapon"] = "奈杰尔武器",
        ["Sarge M60 replacement"] = "萨奇 M60 替换武器",
        ["Uses weapon models and weapon-bone animations only; player hand skeletons are never replaced. The Slingshot has no weapon-side reload clip in the recovered build. Requires rebuild."] = "仅使用武器模型和武器骨骼动画；不会替换玩家手部骨骼。恢复的旧版本中，弹弓没有武器侧装填动画。需要重新构建。",
        ["Nigel Imported Rifle (Prototype)"] = "奈杰尔导入步枪（原型）",
        ["Replaces Nigel's base rifle mesh with the imported no-hammers model. Hands, player skeleton, and default animations remain unchanged. Requires rebuild."] = "使用导入的无击锤模型替换奈杰尔的基础步枪网格。手部、玩家骨骼和默认动画保持不变。需要重新构建。",
        ["Hide falling blocks"] = "隐藏掉落方块",
        ["Hide beam"] = "隐藏光柱",
        ["Hide impact VFX"] = "隐藏命中特效",
        ["Hide lava/water plane"] = "隐藏熔岩／水面",
        ["Hide teammate name background"] = "隐藏队友名称背景",
        ["Enable Time Assault"] = "启用时间突袭",
        ["Auto casual queue"] = "自动加入休闲队列",
        ["Font override (Edo SZ)"] = "字体替换（Edo SZ）",
        ["Local build preview"] = "本地构建预览",
        ["Directional camera motion blur compiled for Unity 5.1. Strength controls the maximum streak length; center focus preserves clarity around the crosshair. Requires rebuild."] = "为 Unity 5.1 编译的方向性镜头运动模糊。强度控制最大拖影长度；中心清晰度可保持准星附近清楚。需要重新构建。",
        ["Applies full-resolution sharpening and lightweight color grading to the world camera. Neutral values preserve the original colors. Requires rebuild."] = "对世界镜头应用全分辨率锐化和轻量色彩分级。中性数值会保留原始颜色。需要重新构建。",
        ["Draws occlusion-aware edges from the camera depth and normal texture. This first experiment outlines scene geometry rather than selected players or objectives. Requires rebuild."] = "根据镜头深度和法线纹理绘制考虑遮挡的边缘。此初始实验会勾勒场景几何体，而不是指定玩家或目标。需要重新构建。",
        ["Height"] = "高度",
        ["Automatically joins the casual matchmaking queue as soon as you enter a custom game lobby, and auto-accepts the match popup when a game is found — pulling you out of the custom game into the casual match."] = "进入自定义游戏大厅后自动加入休闲匹配队列，并在找到比赛时自动接受，将你从自定义游戏带入休闲比赛。",
        ["Forces the Time Assault menu entry to stay visible and enabled even when the live server disables it. Uses the game's existing Time Trial UI and `StartTimeTrial()` network path so the mode can be tested if the backend still accepts it. Requires rebuild."] = "即使在线服务器禁用该功能，也强制显示并启用“时间突袭”菜单。它使用游戏现有的计时挑战界面和 `StartTimeTrial()` 网络路径，以便在后端仍支持时进行测试。需要重新构建。",
        ["Override the map's environmental lighting preset. \"Default\" uses the map's own preset. Cycle through options in-game via F8."] = "覆盖地图的环境光照预设。“默认”使用地图自身预设。可在游戏中按 F8 循环切换。",
        ["Reduces CPU cost on device-heavy maps. Skips distant device health bars entirely, skips full-HP devices each frame, and short-circuits the healthbar update loop when nothing has changed. Throttles minimap, team-overlay WSI, and gravity trap updates, and fully disables fan animators and front player indicators. Keeps player, base, shield-generator, objective, and other always-relevant health bars unaffected. Requires rebuild."] = "降低设备密集地图的 CPU 占用：跳过远处和满生命值设备的生命条，在无变化时提前结束更新，并降低小地图、队伍覆盖层 WSI 和重力陷阱的更新频率，同时禁用风扇动画和前方玩家指示器。玩家、基地、护盾发生器和目标等重要生命条不受影响。需要重新构建。",
        ["Changes the color of friendly and enemy indicators — nameplates, health bars, hit effects. Use the preset buttons for colors from past community seasons, or pick your own."] = "更改友方和敌方指示器的颜色，包括名称、生命条和命中特效。可使用往期社区赛季的预设颜色，也可自行选择。",
        ["Changes the health bar and name color of friendly players when their health drops below a configurable threshold. The direction indicator shows where the low-health ally is on screen."] = "当友方玩家生命值低于设定阈值时，更改其生命条和名称颜色。方向指示器会显示低生命值队友在屏幕上的方向。",
        ["Customizes floating damage and heal numbers in combat. Combine options merge rapid-fire hits into one number until it fades. Minimum heal filters out heals below the set threshold."] = "自定义战斗中的浮动伤害和治疗数字。合并选项会将短时间内的连续数值合并；最小治疗量会过滤低于阈值的治疗。",
        ["Custom weapon reticle. Colors change by damage state: idle / at full damage range / beyond max range. Size scales the whole reticle, spread controls bloom while firing, line thickness thickens line-based reticles, and gap tightens or loosens crosshair arms."] = "自定义武器准星。颜色会根据伤害距离状态变化：待机、完整伤害距离、超过最大距离。大小控制整体缩放，扩散控制射击散布，线条粗细和间距控制准星形状。",
        ["Countdown timer for the shield buff bar. Circle mode draws a clock-style ring, numeric shows a number, off hides it. Offsets move the element relative to its default screen position."] = "护盾增益条的倒计时器。圆形模式显示时钟环，数字模式显示数值，关闭则隐藏。偏移量用于调整其屏幕位置。",
        ["Overrides the camera field of view and ADS sensitivity. Higher FOV gives a wider view at the cost of less zoom. Weapon model FOV controls how large the gun appears in first-person."] = "覆盖镜头视野和瞄准灵敏度。更高的 FOV 可获得更宽视野，但缩放效果会降低。武器模型 FOV 控制第一人称武器的显示大小。",
        ["Shows each teammate's current HP next to their name in the team panel while you are alive."] = "存活时，在队伍面板的队友名称旁显示其当前生命值。",
        ["Shows friendly health bars when spectating teammates during the death cam."] = "死亡镜头观看队友时显示友方生命条。",
        ["Shows the enemy health bar whenever your crosshair is aimed at them, even if they haven't taken damage yet."] = "准星瞄准敌人时始终显示其生命条，即使对方尚未受到伤害。",
        ["Shows a directional indicator when a heal or damage event lands — separate from the floating numbers and larger in scale. Damage indicator color supports __DEFAULT__ to keep the game's built-in color."] = "受到治疗或伤害时显示方向指示器。该指示器独立于浮动数字且尺寸更大。伤害指示器颜色可使用 __DEFAULT__ 保留游戏默认颜色。",
        ["Scales the healthbar and name label shown above all units (friendlies and enemies). Use values below 1.0 to shrink them for a cleaner view. Adjustable in-game via F8."] = "缩放所有单位头顶的生命条和名称。小于 1.0 可缩小界面，使画面更简洁。可在游戏中按 F8 调整。",
        ["Scales the world-space indicators (WSI) shown above units in the game world. Adjustable in-game via F8."] = "缩放游戏世界中单位头顶的世界空间指示器（WSI）。可在游戏中按 F8 调整。",
        ["Adds a tall vertical beam above the capture objective so it is visible from anywhere on the map. No additional options — this is a simple enable/disable toggle."] = "在占领目标上方添加高耸光柱，使其在地图任何位置都清晰可见。此功能仅需启用或禁用。",
        ["Removes the dark rectangle shown behind teammate names in the top-left team HUD. Uses a runtime UI heuristic and requires rebuild to apply."] = "移除左上角队伍界面中队友名称后的深色背景。使用运行时界面识别，需要重新构建后生效。",
        ["Suppress explosion and impact visual effects (bombs, rockets, grenades, cannons, etc.). Only the visual particles are hidden — sound and damage still apply."] = "隐藏炸弹、火箭、手雷和火炮等爆炸与命中的视觉特效。仅隐藏粒子效果，声音和伤害保持不变。",
        ["Only enable if you have high ping. Blocks and devices appear on your screen immediately when placed, without waiting for server confirmation — eliminating the visual delay caused by network latency."] = "仅建议高延迟玩家启用。放置方块和设备时无需等待服务器确认即可立即显示，从而消除网络延迟造成的视觉等待。",
        ["Disables the forced-crouch behaviour that triggers when the ceiling is too low to stand. Can also be toggled in-game via the runtime menu."] = "禁用天花板过低时触发的强制蹲伏。也可通过游戏内运行时菜单切换。",
        ["Restores the stylized Edo SZ font to kill feed messages and center-screen notices (e.g. \"You've killed...\", \"Our cube is under attack!\"). Requires a feature bundle rebuild."] = "为击杀信息和屏幕中央提示恢复 Edo SZ 风格字体。需要重新构建功能包。",
        ["Replaces the overhead health bars with the segmented style from the beta version of the game. Requires the game to be installed — textures are copied into the game's CustomTextures folder on save."] = "将头顶生命条替换为游戏测试版的分段样式。需要已安装游戏；保存时纹理会复制到游戏的 CustomTextures 文件夹。",
        ["Restyles the in-match scoreboard to match the old game's visual style using extracted legacy art and a runtime relayout. Because the current live match data only exposes K / D / A, the old warfare / construction / tactics / healing stat buckets cannot be restored exactly. Requires rebuild."] = "使用旧版素材和运行时重新布局，将比赛计分板恢复为旧版视觉风格。当前比赛数据仅提供击杀／死亡／助攻，因此无法完整恢复旧版的战斗、建造、战术和治疗分类。需要重新构建。",
        ["Analyze to read"] = "分析后读取",
        ["Analyzed"] = "已分析",
        ["Not analyzed"] = "未分析",
        ["Analysis failed"] = "分析失败",
        ["Delete failed"] = "删除失败",
        ["Error"] = "错误",
        ["Launch failed"] = "启动失败",
        ["Update failed"] = "更新失败",
        ["Required update"] = "必须更新",
        ["Update available"] = "有可用更新"
    };

    private static readonly IReadOnlyDictionary<string, string> Fragments = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Block N Load Community Fixes V2 - "] = "Block N Load 社区修复 V2 - ",
        ["Game path: "] = "游戏路径：",
        ["Detection: "] = "检测方式：",
        ["Launcher version: "] = "启动器版本：",
        ["Selected server: "] = "已选服务器：",
        ["Server list: "] = "服务器列表：",
        ["Settings profile: "] = "设置配置：",
        ["Latest replay: "] = "最新录像：",
        ["Replay recording: "] = "录像录制：",
        ["Target: "] = "目标：",
        ["No replay captures found. Finish a match and refresh."] = "未找到比赛录像。完成一场比赛后请刷新。",
        [" replay capture(s) found."] = " 个比赛录像。",
        ["Analyzing "] = "正在分析 ",
        ["Analyzed "] = "已分析 ",
        [" replays…"] = " 个录像…",
        [" replays."] = " 个录像。",
        ["Replay analysis failed."] = "录像分析失败。",
        ["Delete replay:"] = "删除录像：",
        [" selected replays?"] = " 个选中的录像吗？",
        ["A new version is ready to install. Review the changes below."] = "新版本已可安装。请在下方查看更新内容。",
        ["This version is required to continue. Please install it now."] = "必须安装此版本才能继续。请立即安装。",
        ["Download cancelled."] = "下载已取消。",
        ["Downloading game..."] = "正在下载游戏…",
        ["Downloading audio dependencies..."] = "正在下载音频依赖…",
        ["Extracting game files..."] = "正在解压游戏文件…",
        ["Extracting audio dependencies..."] = "正在解压音频依赖…",
        ["Please choose an installation folder."] = "请选择安装文件夹。",
        ["Path not found:"] = "找不到路径：",
        ["Key, name, and host are required."] = "必须填写键、名称和主机。",
        ["The no-Steam fix files are not present. Apply them now?"] = "缺少免 Steam 修复文件。现在应用吗？",
        ["Recommended Settings Applied"] = "已应用推荐设置",
        ["This launcher update reset the active feature settings to Recommended Settings."] = "本次启动器更新已将当前功能设置重置为推荐设置。",
        ["Use the Settings profile dropdown on the main screen if you want to restore your Personal Settings."] = "如需恢复个人设置，请使用主界面的“设置配置”下拉菜单。",
        [" settings to see the changes."] = " 设置以查看更改。",
        [" feature config(s)."] = " 个功能配置。",
        ["Import complete"] = "导入完成",
        ["Export complete"] = "导出完成",
        ["Import failed"] = "导入失败",
        ["Export failed"] = "导出失败",
        ["Save failed"] = "保存失败",
        ["Setup Failed"] = "设置失败",
        ["Setup Warning"] = "设置警告"
    };

    public static void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (!IsChinese())
            return;

        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        timer.Tick += (_, _) =>
        {
            foreach (var window in desktop.Windows)
                Apply(window);
        };
        timer.Start();
    }

    public static string Translate(string value)
    {
        if (!IsChinese())
            return value;
        if (Text.TryGetValue(value, out var translated))
            return translated;

        foreach (var (english, chinese) in Fragments)
            value = value.Replace(english, chinese, StringComparison.Ordinal);
        return value;
    }

    private static bool IsChinese()
    {
        var requested = Environment.GetEnvironmentVariable("BNL_LANGUAGE");
        var culture = string.IsNullOrWhiteSpace(requested)
            ? CultureInfo.CurrentUICulture.Name
            : requested;
        return culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }

    private static void Apply(Window window)
    {
        window.Title = Translate(window.Title ?? string.Empty);
        TranslateControl(window);
        foreach (var control in window.GetLogicalDescendants().OfType<Control>())
            TranslateControl(control);
    }

    private static void TranslateControl(Control control)
    {
        switch (control)
        {
            case TextBlock text when !string.IsNullOrEmpty(text.Text):
                text.Text = Translate(text.Text);
                break;
            case TextBox textBox when !string.IsNullOrEmpty(textBox.Watermark):
                textBox.Watermark = Translate(textBox.Watermark);
                break;
            case HeaderedContentControl headered when headered.Header is string header:
                headered.Header = Translate(header);
                break;
            case ContentControl content when content.Content is string value:
                content.Content = Translate(value);
                break;
            case DataGrid grid:
                foreach (var column in grid.Columns)
                    if (column.Header is string columnHeader)
                        column.Header = Translate(columnHeader);
                break;
        }
    }
}
