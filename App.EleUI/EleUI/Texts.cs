using System.Collections.Generic;
using System.Linq;

namespace App.EleUI
{
    /// <summary>
    /// Supportted cultures.
    /// </summary>
    public enum Culture
    {
        Chinese,
        English,
    }

    /// <summary>
    /// Texts resource.
    /// Texts.Current.XXXX
    /// Texts.SetCurrent(Culture culture)
    /// </summary>
    public class Texts
    {
        public Culture Culture { get; set; }

        public string Select { get; set; }
        public string PleaseSelect { get; set; }
        public string SelectIcon { get; set; }
        public string ClearIcon { get; set; }
        public string SelectFile { get; set; }
        public string CannotEmpty { get; set; }

        public string Prompt { get; set; }
        public string PleaseInput { get; set; }
        public string PleaseInputContent { get; set; }
        public string Confirm { get; set; }
        public string Cancel { get; set; }
        public string Yes { get; set; }
        public string No { get; set; }

        public string Query { get; set; }
        public string Reset { get; set; }
        public string FilterConditions { get; set; }

        public string Details { get; set; }
        public string View { get; set; }
        public string Edit { get; set; }
        public string Delete { get; set; }
        public string New { get; set; }
        public string Export { get; set; }
        public string Import { get; set; }
        public string BatchModify { get; set; }
        public string Operation { get; set; }

        public string List { get; set; }
        public string EmptyData { get; set; }
        public string PageSizeLabel { get; set; }
        public string TotalPrefix { get; set; }
        public string TotalSuffix { get; set; }
        public string SerialNumber { get; set; }
        public string Name_ { get; set; }
        public string Level { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string InitiatorOrg { get; set; }
        public string Progress { get; set; }
        public string DateTime_ { get; set; }
        public string Date_ { get; set; }
        public string Time_ { get; set; }

        public string SaveSuccess { get; set; }
        public string FieldNameCannotParse { get; set; }
        public string PleaseSelectOrInput { get; set; }
        public string OpenSelectWindow { get; set; }
        public string LoadingText { get; set; }
        public string NoMoreData { get; set; }
        public string AddAttribute { get; set; }
        public string Field_ { get; set; }
        public string Value_ { get; set; }
        public string NoAttribute { get; set; }
        public string Clear { get; set; }
        public string DeleteImage { get; set; }
        public string UploadNewImage { get; set; }
        public string UploadedPreview { get; set; }
        public string SelectDate { get; set; }
        public string SelectTime { get; set; }
        public string SelectDateTime { get; set; }
        public string SelectStartDate { get; set; }
        public string SelectEndDate { get; set; }
        public string NoDataNow { get; set; }
        public string AddNewItem { get; set; }
        public string ClickToUploadOrDragHere { get; set; }

        private static readonly List<Texts> _texts = new()
        {
            new Texts
            {
                Culture = Culture.Chinese,
                Select = "选择",
                PleaseSelect = "请选择",
                SelectIcon = "选择图标",
                ClearIcon = "清除图标",
                SelectFile = "选择文件",
                CannotEmpty = "不能为空",
                Prompt = "提示",
                PleaseInput = "请输入",
                PleaseInputContent = "请输入内容",
                Confirm = "确定",
                Cancel = "取消",
                Yes = "是",
                No = "否",
                Query = "查询",
                Reset = "重置",
                FilterConditions = "筛选条件",
                Details = "详情",
                View = "查看",
                Edit = "编辑",
                Delete = "删除",
                New = "新增",
                Export = "导出",
                Import = "导入",
                BatchModify = "批量修改",
                Operation = "操作",
                List = "列表",
                EmptyData = "暂无数据",
                PageSizeLabel = "每页记录数",
                TotalPrefix = "共",
                TotalSuffix = "条",
                SerialNumber = "序号",
                Name_ = "名称",
                Level = "级别",
                StartDate = "开始日期",
                EndDate = "结束日期",
                InitiatorOrg = "发起组织",
                Progress = "进度",
                DateTime_ = "日期时间",
                Date_ = "日期",
                Time_ = "时间",
                SaveSuccess = "保存成功",
                FieldNameCannotParse = "无法解析字段名称",
                PleaseSelectOrInput = "请选择或输入",
                OpenSelectWindow = "打开选择窗口",
                LoadingText = "加载中...",
                NoMoreData = "",
                AddAttribute = "新增属性",
                Field_ = "字段",
                Value_ = "值",
                NoAttribute = "暂无属性",
                Clear = "清空",
                DeleteImage = "删除图片",
                UploadNewImage = "上传图片",
                UploadedPreview = "已上传预览",
                SelectDate = "选择日期",
                SelectTime = "选择时间",
                SelectDateTime = "选择日期时间",
                SelectStartDate = "开始日期",
                SelectEndDate = "结束日期",
                NoDataNow = "暂无数据",
                AddNewItem = "新增项",
                ClickToUploadOrDragHere = "点击上传或拖拽到此处"
            },
            new Texts
            {
                Culture = Culture.English,
                Select = "Select",
                PleaseSelect = "Please select",
                SelectIcon = "Select Icon",
                ClearIcon = "Clear icon",
                SelectFile = "Select file",
                CannotEmpty = "Cannot be empty",
                Prompt = "Prompt",
                PleaseInput = "Please input",
                PleaseInputContent = "Please input content",
                Confirm = "OK",
                Cancel = "Cancel",
                Yes = "Yes",
                No = "No",
                Query = "Query",
                Reset = "Reset",
                FilterConditions = "Filters",
                Details = "Details",
                View = "View",
                Edit = "Edit",
                Delete = "Delete",
                New = "New",
                Export = "Export",
                Import = "Import",
                BatchModify = "Batch modify",
                Operation = "Operation",
                List = "List",
                EmptyData = "No data",
                PageSizeLabel = "Per page",
                TotalPrefix = "Total",
                TotalSuffix = "",
                SerialNumber = "#",
                Name_ = "Name",
                Level = "Level",
                StartDate = "Start date",
                EndDate = "End date",
                InitiatorOrg = "Initiator",
                Progress = "Progress",
                DateTime_ = "DateTime",
                Date_ = "Date",
                Time_ = "Time",
                SaveSuccess = "Saved",
                FieldNameCannotParse = "Cannot parse field name",
                PleaseSelectOrInput = "Please select or input ",
                OpenSelectWindow = "Open selector",
                LoadingText = "Loading...",
                NoMoreData = "No more data",
                AddAttribute = "Add attribute",
                Field_ = "Field",
                Value_ = "Value",
                NoAttribute = "No attributes",
                Clear = "Clear",
                DeleteImage = "Delete image",
                UploadNewImage = "Upload image",
                UploadedPreview = "Uploaded preview",
                SelectDate = "Select date",
                SelectTime = "Select time",
                SelectDateTime = "Select date time",
                SelectStartDate = "Start date",
                SelectEndDate = "End date",
                NoDataNow = "No data",
                AddNewItem = "Add new item",
                ClickToUploadOrDragHere = "Click to upload or drag here"
            }
        };

        public static Texts Current { get; private set; } = _texts[0];

        public static void SetCurrent(Culture type)
        {
            Current = _texts.Find(x => x.Culture == type) ?? Current;
        }
    }
}
