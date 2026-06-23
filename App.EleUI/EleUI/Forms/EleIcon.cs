using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;

namespace App.EleUI
{
    /// <summary>
    /// Element Plus 图标名称枚举（来自 @element-plus/icons-vue）。
    /// 枚举值与 Vue 组件名一致（PascalCase），参考：https://element-plus.org/zh-CN/component/icon
    /// </summary>
    public enum EleIcons
    {
        /// <summary>不指定图标（使用子内容或空）</summary>
        None = 0,

        // System（官网分类）
        Plus, Minus, CirclePlus, Search, Female, Male, Aim,
        House, FullScreen, Loading, Link, Service, Pointer, Star,
        Notification, Connection, ChatDotRound, Setting, Clock, Position,
        Discount, Odometer, ChatSquare, ChatRound, ChatLineRound, ChatLineSquare,
        ChatDotSquare, View, Hide, Unlock, Lock, RefreshRight, RefreshLeft,
        Refresh, Bell, MuteNotification, User, Check, CircleCheck, Warning,
        CircleClose, Close, PieChart, More, Compass, Filter, Switch,
        Select, SemiSelect, CloseBold, EditPen, Edit, Message, MessageBox,
        TurnOff, Finished, Delete, Crop, SwitchButton, Operation, Open,
        Remove, ZoomOut, ZoomIn, InfoFilled, CircleCheckFilled, SuccessFilled,
        WarningFilled, CircleCloseFilled, QuestionFilled, WarnTriangleFilled,
        UserFilled, MoreFilled, Tools, HomeFilled, Menu, UploadFilled, Avatar,
        HelpFilled, Share, StarFilled, Comment, Histogram, Grid, Promotion,
        DeleteFilled, RemoveFilled, CirclePlusFilled, NoSmoking, Smoking,

        // Arrow（官网分类）
        ArrowLeft, ArrowUp, ArrowRight, ArrowDown,
        ArrowLeftBold, ArrowUpBold, ArrowRightBold, ArrowDownBold,
        DArrowRight, DArrowLeft, Download, Upload, Top, Bottom,
        Back, Right, TopRight, TopLeft, BottomRight, BottomLeft,
        Sort, SortUp, SortDown, Rank,

        // Document（官网分类）
        DocumentAdd, Document, Notebook, Tickets, Memo, Collection, Postcard,
        ScaleToOriginal, SetUp, DocumentDelete, DocumentChecked, DataBoard,
        DataAnalysis, CopyDocument, FolderChecked, Files, Folder, FolderDelete,
        FolderRemove, FolderOpened, DocumentCopy, DocumentRemove, FolderAdd,
        FirstAidKit, Reading, DataLine, Management, Checked, Ticket, Failed,
        TrendCharts, List,

        // Media（官网分类）
        Microphone, Mute, Mic, VideoPause, VideoCamera, VideoPlay, Headset,
        Monitor, Film, Camera, Picture, PictureRounded, Iphone, Cellphone,
        VideoCameraFilled, PictureFilled, Platform, CameraFilled,

        // Traffic（官网分类）
        Location, LocationInformation, DeleteLocation, Coordinate, Bicycle,
        OfficeBuilding, School, Guide, AddLocation, MapLocation, Place,
        LocationFilled, Van,

        // Food（官网分类）
        Watermelon, Pear, Mug, GobletSquareFull, GobletFull, KnifeFork,
        Sugar, Bowl, MilkTea, Lollipop, Coffee, Chicken, Dish, IceTea,
        ColdDrink, CoffeeCup, DishDot, IceDrink, IceCream, Dessert,
        IceCreamSquare, ForkSpoon, IceCreamRound, Food, HotWater,
        Grape, Fries, Apple, Burger, Goblet, GobletSquare, Orange, Cherry,

        // Items（官网分类）
        Printer, Calendar, CreditCard, Box, Money, Refrigerator, Cpu,
        Football, Brush, Suitcase, SuitcaseLine, Umbrella, AlarmClock,
        Medal, GoldMedal, Present, Mouse, Watch, QuartzWatch, Magnet,
        Help, Soccer, ToiletPaper, ReadingLamp, Paperclip, MagicStick,
        Basketball, Baseball, Coin, Goods, Sell, SoldOut, Key,
        ShoppingCart, ShoppingCartFull, ShoppingTrolley, Phone, Scissor,
        Handbag, ShoppingBag, Trophy, TrophyBase, Stopwatch, Timer,
        CollectionTag, TakeawayBox, PriceTag, Wallet, Opportunity,
        PhoneFilled, WalletFilled, GoodsFilled, Flag, BrushFilled,
        Briefcase, Stamp,

        // Weather（官网分类）
        Sunrise, Sunny, Ship, MostlyCloudy, PartlyCloudy, Sunset,
        Drizzling, Pouring, Cloudy, Moon, MoonNight, Lightning,

        // Other（官网分类）
        ChromeFilled, Eleme, ElemeFilled, ElementPlus, Shop, SwitchFilled, WindPower,

        // Weather（语义别名）
        WeatherSunrise, WeatherSunny, WeatherCloudy, WeatherMostlyCloudy, WeatherPartlyCloudy,
        WeatherSunset, WeatherRain, WeatherLightRain, WeatherMoon, WeatherMoonNight,
        WeatherLightning, WeatherWind, WeatherShip,

        // Map（语义别名）
        MapPin, MapPoint, MapGuide, MapCoordinate, MapPlace, MapArea,
        MapBuilding, MapSchool, MapBike, MapCar, MapShip, MapFilled,
    }

    /// <summary>
    /// Element Plus 图标。输出 &lt;el-icon&gt; 元素，内部包裹图标组件。
    /// 参考：https://element-plus.org/zh-CN/component/icon
    /// 输出示例：
    /// <code>&lt;el-icon size="24" color="#409EFF"&gt;&lt;Edit /&gt;&lt;/el-icon&gt;</code>
    /// 注意：图标组件需通过 ElementPlusIconsVue 全局注册后方可使用。
    /// </summary>
    [HtmlTargetElement("EleIcon")]
    public class EleIcon : EleControl
    {
        /// <summary>获取图标组件名</summary>
        static string GetIconName(EleIcons name)
        {
            return name switch
            {
                EleIcons.WeatherSunrise => nameof(EleIcons.Sunrise),
                EleIcons.WeatherSunny => nameof(EleIcons.Sunny),
                EleIcons.WeatherCloudy => nameof(EleIcons.Cloudy),
                EleIcons.WeatherMostlyCloudy => nameof(EleIcons.MostlyCloudy),
                EleIcons.WeatherPartlyCloudy => nameof(EleIcons.PartlyCloudy),
                EleIcons.WeatherSunset => nameof(EleIcons.Sunset),
                EleIcons.WeatherRain => nameof(EleIcons.Pouring),
                EleIcons.WeatherLightRain => nameof(EleIcons.Drizzling),
                EleIcons.WeatherMoon => nameof(EleIcons.Moon),
                EleIcons.WeatherMoonNight => nameof(EleIcons.MoonNight),
                EleIcons.WeatherLightning => nameof(EleIcons.Lightning),
                EleIcons.WeatherWind => nameof(EleIcons.WindPower),
                EleIcons.WeatherShip => nameof(EleIcons.Ship),

                EleIcons.MapPin => nameof(EleIcons.MapLocation),
                EleIcons.MapPoint => nameof(EleIcons.Location),
                EleIcons.MapGuide => nameof(EleIcons.Guide),
                EleIcons.MapCoordinate => nameof(EleIcons.Coordinate),
                EleIcons.MapPlace => nameof(EleIcons.Place),
                EleIcons.MapArea => nameof(EleIcons.LocationInformation),
                EleIcons.MapBuilding => nameof(EleIcons.OfficeBuilding),
                EleIcons.MapSchool => nameof(EleIcons.School),
                EleIcons.MapBike => nameof(EleIcons.Bicycle),
                EleIcons.MapCar => nameof(EleIcons.Van),
                EleIcons.MapShip => nameof(EleIcons.Ship),
                EleIcons.MapFilled => nameof(EleIcons.LocationFilled),
                _ => name.ToString()
            };
        }

        /// <summary>图标名称（Element Plus 图标枚举）。默认 None 时使用子内容。</summary>
        [HtmlAttributeName("Name")]
        public EleIcons Name { get; set; } = EleIcons.None;

        /// <summary>图标尺寸（像素数字或 CSS 字符串），例如 "24" 或 "1.5em"。对应 el-icon 的 size 属性。</summary>
        [HtmlAttributeName("Size")]        public string Size { get; set; }

        /// <summary>图标颜色（CSS 颜色值），例如 "#409EFF"、"red"、"var(--el-color-primary)"。对应 el-icon 的 color 属性。</summary>
        [HtmlAttributeName("Color")]        public string Color { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output))
                return;
            output.TagName = "el-icon";
            output.TagMode = TagMode.StartTagAndEndTag;
            AddCommonAttributes(context, output);

            // size
            if (!string.IsNullOrEmpty(Size))
                output.Attributes.SetAttribute("size", Size);

            // color
            if (!string.IsNullOrEmpty(Color))
                output.Attributes.SetAttribute("color", Color);

            // name
            if (Name != EleIcons.None)
            {
                // Use dynamic component to avoid conflicts with native tags like link/view/filter/switch.
                output.Content.SetHtmlContent($"<component :is=\"'{GetIconName(Name)}'\"></component>");
            }
            else
            {
                // Name=None 时使用子内容（须通过 @Html.Raw 传入 Vue 组件名，如 @Html.Raw("<Edit />")）
                var childContent = await output.GetChildContentAsync();
                output.Content.SetHtmlContent(childContent.GetContent());
            }
        }
    }
}
