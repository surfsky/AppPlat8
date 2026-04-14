# 隐患排查相关页面


# 相关代码

```csharp

文本、日期、布尔、枚举
    <EleInput For="Item.CheckItemText" Label="检查项内容" Enabled="false" FillRow="true"></EleInput>
    <EleDatePicker For="Item.RectifyDt" Label="整改日期"></EleDatePicker>        
    <EleSwitch For="Item.IsIn141" Label="录入141"></EleSwitch>
    <EleSelect For="Item.Status" Label="状态"></EleSelect>

图片、多张图片
    <EleImageUpload For="Item.Photo" Label="照片" FillRow="true"></EleImageUpload>
    <EleImageUpload For="Item.ImageUrls" Label="隐患图片" Multi="true" FillRow="true"></EleImageUpload>

用户选择、对象选择
    <EleSelector For="Item.UserId" TextFor="Item.UserName" Label="检查人" Enabled="false" FillRow="true" PopupUrl="/Shared/UserSelector?multi=0"></EleSelector>
    <EleSelector For="Item.CheckObjectId" TextFor="Item.ObjectName" Label="检查对象" Enabled="false" FillRow="true" PopupUrl="CheckObjects?md=select"></EleSelector>

```