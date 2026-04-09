using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Utils;

namespace App.DAL
{
    //-------------------------------------------------------
    // 141 系统预设枚举值
    //-------------------------------------------------------
    [UI("检查", "风险等级")]
    public enum CheckRiskLevel
    {
        [UI("无风险")] None = 0,
        [UI("低风险")] Low = 1,
        [UI("中风险")] Medium = 2,
        [UI("高风险")] High = 3
    }

    [UI("检查", "检查对象四色")]
    public enum CheckRiskColor
    {
        [UI("绿色")] Green = 0,
        [UI("黄色")] Yellow = 1,
        [UI("橙色")] Orange = 2,
        [UI("红色")] Red = 3
    }

    [UI("检查", "检查对象类型")]
    public enum CheckObjectType
    {
        [UI("场所")] Place = 0,
        [UI("企业")] Enterprise = 1,
        [UI("机构")] Orgnization = 2,
        [UI("个体工商户")] Person = 3,
        [UI("其他")] Other = 4,
    }

    [UI("检查", "建筑类型")]
    public enum CheckBuildingType
    {
        [UI("普通建筑")] Normal = 0,
        [UI("商场")] Mall = 1,
        [UI("医院")] Hospital = 2,
        [UI("学校")] School = 3,
        [UI("其他")] Other = 4,
    }

    [UI("检查", "检查领域")]
    public enum CheckScope
    {
        [UI("消防安全")] FireSafety = 0,
        [UI("工矿")] Industry = 1,
        [UI("城市运行")] CityOperation = 2,
        [UI("道路交通")] Traffic = 3,
        [UI("建设施工")] Construction = 4,
        [UI("旅游")] Tourism = 5,
        [UI("危险化学品")] HazardousChemicals = 6,
        [UI("涉海涉渔")] Maritime = 7,
        [UI("其他")] Other = 8,
    }

    //-------------------------------------------------------
    // 自有枚举值
    //-------------------------------------------------------
    // 2、企业规模填写：①规下，②规上亿元以下，③规上亿元以上；
    [UI("检查", "检查对象规模")]
    public enum CheckObjectScale
    {
        [UI("小微")] SmallMicro = 0,
        [UI("规下")] BelowScale = 1,
        [UI("规上")] AboveScale = 2,
        [UI("规上亿元")] AboveScaleYi = 3,
    }

    //3、企业标准化创建情况：①三级，②小微，③未创建；
    [UI("检查", "标准化状态")]
    public enum CheckStandardizationStatus
    {
        [UI("未创建")] None = 3,
        [UI("小微")] SmallMicro = 2,
        [UI("三级")] Level3 = 1
    }

    //4、厂房使用权类型填写：①集聚区内独立厂房，②集聚区外独立厂房，③集聚区内厂中厂自持，④集聚区外厂中厂自持，⑤集聚区内厂中厂承租，⑥集聚区外厂中厂承租，⑦园中园自持，⑧园中园承租，⑨合用场所（200m²以上）；
    [UI("检查", "厂房使用权类型")]
    public enum CheckFactoryUsageType
    {
        [UI("集聚区内独立厂房")] ClusterInIndependent = 1,
        [UI("集聚区外独立厂房")] ClusterOutIndependent = 2,
        [UI("集聚区内厂中厂自持")] ClusterInSelf = 3,
        [UI("集聚区外厂中厂自持")] ClusterOutSelf = 4,
        [UI("集聚区内厂中厂承租")] ClusterInRent = 5,
        [UI("集聚区外厂中厂承租")] ClusterOutRent = 6,
        [UI("园中园自持")] ParkSelf = 7,
        [UI("园中园承租")] ParkRent = 8,
        [UI("合用场所（200m²以上）")] Mix = 9
    }


    // 5、厂房房屋结构填写：①砖木结构，②砖混结构，③钢筋混凝土结构，④钢结构；
    [UI("检查", "厂房房屋结构")]
    public enum CheckBuildingStructure
    {
        [UI("砖木结构")]      BrickWood = 1,
        [UI("砖混结构")]      BrickConcrete = 2,
        [UI("钢筋混凝土结构")] Concrete = 3,
        [UI("钢结构")]        Steel = 4,
    }

    // 6、风险行业类型填写：  ①胶印，②凹版印刷，③其他印刷， ④纸制品业，⑤珍珠棉，⑥塑编，⑦其他塑料制品业，⑧纺织业，⑨机械行业，⑩金属制品业，⑪商贸行业，⑫木材行业，⑬其他行业； 
    [UI("检查", "风险行业类型")]
    public enum CheckIndustryType
    {
        [UI("胶印")] PrintingOffset = 1,
        [UI("凹版印刷")] PrintingGravure = 2,
        [UI("其他印刷")] PrintingOther = 3,
        [UI("纸制品业")] Paper = 4,
        [UI("珍珠棉")] PearlCotton = 5,
        [UI("塑编")] PlasticWeaving = 6,
        [UI("其他塑料制品业")] PlasticOther = 7,
        [UI("纺织业")] Textile = 8,
        [UI("机械行业")] Machinery = 9,
        [UI("金属制品业")] Metal = 10,
        [UI("商贸行业")] Commerce = 11,
        [UI("木材行业")] Wood = 12,
        [UI("其他行业")] Other = 13
    }

    //7、行业风险填写：（1）“三场所三企业”①船舶修造、②粉尘涉爆、③喷涂作业、④有限空间作业、⑤涉氨制冷、⑥高温熔融金属；（2）⑦易燃企业、⑧酒类生产、⑨危化品使用、⑩电雕：⑪电镀、⑫塑料、⑬纺织印染、⑭皮革、⑮海绵和珍珠棉生产、⑯锂电池生产、⑰矿山企业、⑱一般轻工、⑲喷塑企业、⑳导热油企业、㉑家电制造、㉒竹木加工、㉓制鞋、㉔喷漆；
    [UI("检查", "行业风险类型")]
    public enum CheckIndustryRiskType
    {
        [UI("船舶修造")] ShipRepair = 1,
        [UI("粉尘涉爆")] DustExplosion = 2,
        [UI("喷涂作业")] SprayPainting = 3,
        [UI("有限空间作业")] ConfinedSpace = 4,
        [UI("涉氨制冷")] AmmoniaRefrigeration = 5,
        [UI("高温熔融金属")] HighTempMetal = 6,
        [UI("易燃企业")] Flammable = 7,
        [UI("酒类生产")] Alcohol = 8,
        [UI("危化品使用")] HazardousChemicals = 9,
        [UI("电雕")] ElectricCarving = 10,
        [UI("电镀")] Electroplating = 11,
        [UI("塑料")] Plastic = 12,
        [UI("纺织印染")] TextilePrinting = 13,
        [UI("皮革")] Leather = 14,
        [UI("海绵和珍珠棉生产")] SpongePearlCotton = 15,
        [UI("锂电池生产")] LithiumBattery = 16,
        [UI("矿山企业")] Mining = 17,
        [UI("一般轻工")] GeneralLight = 18,
        [UI("喷塑企业")] SprayPlastic = 19,
        [UI("导热油企业")] HeatTransferOil = 20,
        [UI("家电制造")] HomeAppliance = 21,
        [UI("竹木加工")] WoodProcessing = 22,
        [UI("制鞋")] ShoeMaking = 23,
        [UI("喷漆")] Painting = 24,
    }

    //8、危化品使用量填写：①少量（未设置危化品中间仓库，使用防爆柜）；②一般（设置中间仓库）；③大量（设置中间仓库）。
    [UI("检查", "危化品使用量")]
    public enum CheckHazardousChemicalsUsage
    {
        [UI("无")] None = 0,
        [UI("少量")] Minor = 1,
        [UI("一般")] Average = 2,
        [UI("大量")] Major = 3,
    }

    [UI("检查", "隐患等级")]
    public enum CheckHazardLevel
    {
        [UI("一般隐患")] NormalHazard = 1,
        [UI("重大隐患")] HighHazard = 2,
        [UI("重点问题")] HighProblem = 3
    }

}