EleImageUpload 控件
    见 EleUI/EleForms 页面
    <EleImageUpload For="Item.Photo" Label="图片上传"></EleImageUpload>
    <EleImageUpload For="Item.Photos" Label="多图片上传" Multi="true" MultiLimit="5"></EleImageUpload>



```
    <EleForm Model="form" LabelWidth="150px">
        <Toolbar>
            <EleButton Type="Primary" Command="Search" Icon="Search">查询</EleButton>
            <EleButton Type="Primary" Command="Add" Icon="Plus">新增</EleButton>
            <EleButton Type="Primary" Command="Save" Icon="Plus">保存</EleButton>
            <EleButton Type="Primary" Command="Close" Icon="Plus">关闭</EleButton>
        </Toolbar>

        <EleInput For="Item.Name" Label="文本框"></EleInput>
        <EleInput For="Item.Description" Label="多行文本" Type="TextArea" Rows="4"></EleInput>
        <EleNumber For="Item.Age" Label="数字" Step="1"></EleNumber>
        <EleNumber For="Item.Score" Label="金额" Precision="2" Step="0.1"></EleNumber>
        <EleDatePicker For="Item.Birthday" Label="日期"></EleDatePicker>
        <EleSwitch For="Item.IsEnabled" Label="开关"></EleSwitch>


        <EleLabel Label="List" FillRow="true"></EleLabel>
        <EleRadio For="Item.Gender" Label="单选" Items="男, 女, 未知"></EleRadio>
        <EleRadio For="Item.Gender" Label="单选组" Items="男, 女, 未知" IsButton="true"></EleRadio>
        <EleSelect For="Item.City" Label="下拉" Items="Model.CityList"></EleSelect>
        <EleSelect For="Item.Types" Label="多选" Items="Model.TypeList" Multiple="true"></EleSelect> 

        <EleLabel Label="Tree" FillRow="true"></EleLabel>
        <EleTreeSelect For="Item.OrgId" Label="组织树" Api="/httpapi/orgs/GetAuthOrgTree" ></EleTreeSelect>
        <EleTreeSelect For="Item.DeptId" Label="部门树" Items="Model.DeptTree" ></EleTreeSelect>

        <EleLabel Label="File" FillRow="true"></EleLabel>
        <EleImageUpload For="Item.Photo" Label="图片上传"></EleImageUpload>
        <EleImageUpload For="Item.Photos" Label="多图片上传" Multi="true" MultiLimit="5"></EleImageUpload>
        <EleIconSelect For="Item.Icon" Label="图标选择"></EleIconSelect>

        <EleLabel Label="Popup" FillRow="true"></EleLabel>
        <EleSelector For="Item.UserId" Label="用户选择" TextFor="Item.UserName" Title="选择用户" PopupUrl="/Shared/UserSelector?multi=0"></EleSelector>
    </EleForm>
```