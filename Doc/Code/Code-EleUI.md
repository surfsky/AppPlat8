EleImageUpload 控件
    见 EleUI/EleForms 页面

```html
Form
    <EleForm Model="form" LabelWidth="150px">
        <Toolbar>
            <EleButton Type="Primary" Command="Search" Icon="Search">查询</EleButton>
            <EleButton Type="Primary" Command="Add" Icon="Plus">新增</EleButton>
            <EleButton Type="Primary" Command="Save" Icon="Plus">保存</EleButton>
            <EleButton Type="Primary" Command="Close" Icon="Plus">关闭</EleButton>
        </Toolbar>
    </EleForm>

Label
    <EleLabel Label="List" FillRow="true"></EleLabel>

Input
    <EleInput For="Item.Name" Label="文本框"></EleInput>
    <EleInput For="Item.Description" Label="多行文本" Type="TextArea" Rows="4"></EleInput>

Number
    <EleNumber For="Item.Age" Label="数字" Step="1"></EleNumber>
    <EleNumber For="Item.Score" Label="金额" Precision="2" Step="0.1"></EleNumber>

DatePicker
    <EleDatePicker For="Item.Birthday" Label="日期"></EleDatePicker>
    <EleDatePicker For="Item.CreateTime" Type="DateRange" StartPlaceholder="开始日期" EndPlaceholder="结束日期" Width="600px" Label="创建时间" ></EleDatePicker>
    public IActionResult OnGetData(List<DateTime> createTime)
    {
        var fromDt = createTime[0];
        var toDt = createTime[1];
        ...
    }


Switch
    <EleSwitch For="Item.IsEnabled" Label="开关"></EleSwitch>

Radio
    <EleRadio For="Item.Gender" Label="单选" Items="男, 女, 未知"></EleRadio>
    <EleRadio For="Item.Gender" Label="单选组" Items="男, 女, 未知" IsButton="true"></EleRadio>

Select
    <EleSelect For="Item.City" Label="下拉" Items="Model.CityList"></EleSelect>
    <EleSelect For="Item.Types" Label="多选" Items="Model.TypeList" Multiple="true"></EleSelect> 

TreeSelect
    <EleTreeSelect For="Item.OrgId" Label="组织树" Api="/httpapi/orgs/GetAuthOrgTree" ></EleTreeSelect>
    <EleTreeSelect For="Item.DeptId" Label="部门树" Items="Model.DeptTree" ></EleTreeSelect>

TreePicker
    <EleTreePicker For="Item.OrgId" Label="组织树" Api="/httpapi/orgs/GetAuthOrgTree" ></EleTreePicker>

ImageUpload
    <EleImageUpload For="Item.Photo" Label="图片上传"></EleImageUpload>
    <EleImageUpload For="Item.Photos" Label="多图片上传" Multi="true" MultiLimit="5"></EleImageUpload>

IconSelect
    <EleIconSelect For="Item.Icon" Label="图标选择"></EleIconSelect>

Picker
    <ElePicker For="Item.UserId" Label="用户选择" TextFor="Item.UserName" Title="选择用户" PopupUrl="/Shared/UserSelector?multi=0"></ElePicker>
```

EleTable

```html
    <EleOpColumn Width="120" Fixed="right" Shows="3">
        <Op Command="Edit"   Icon="Edit" Tooltip="编辑" Visible="@Auth.CheckPower(Power.CheckObjectEdit)"></Op>
        <Op Command="Delete" Icon="Delete" Tooltip="删除" Visible="false"></Op>
        <Op Popup="/Checks/CheckFlow?objectId={id}&objectName={name}"   Icon="DocumentChecked" Text="开始排查" Visible="@Auth.CheckPower(Power.CheckNew)"></Op>
        <Op Popup="/Checks/ObjectContacts?objectId={id}&objectName={name}"   Icon="User" Text="联系人" Visible="@Auth.CheckPower(Power.CheckObjectView)"></Op>
        <Op Popup="/Checks/CheckHazards?objectId={id}&objectName={name}"   Icon="WarningFilled" Text="隐患清单" Visible="@Auth.CheckPower(Power.CheckHazardView)"></Op>
        <Op Popup="/Checks/Checks?objectId={id}&objectName={name}"   Icon="DocumentChecked" Text="排查历史" Visible="@Auth.CheckPower(Power.CheckView)"></Op>
        <Op Popup="/Checks/CheckPoints?objectId={id}&objectName={name}"   Icon="DocumentChecked" Text="风险点清单" Visible="@Auth.CheckPower(Power.CheckObjectView)"></Op>
        <Op Popup="/Shared/Atts?uniId=CheckObject-{id}&name={name}"   Icon="Paperclip" Text="附件" Visible="@Auth.CheckPower(Power.CheckObjectView)"></Op>
        <Op Popup="/Checks/CheckObjectEvents?objectId={id}&objectName={name}"   Icon="DocumentChecked" Text="事件事故" Visible="@Auth.CheckPower(Power.CheckObjectView)"></Op>
    </EleOpColumn>
```

EleList

```html
<EleList Label="附件"
        FillRow="true"
        DataHandler="?handler=AttsData&taskId=@taskId"
        PageSize="10"
        Height="240px"
        MinHeight="120px"
        EmptyText="暂无附件"
        LastText=" "
        SortField="Id"
        SortDirection="DESC"
        ScrollClass="flex-1 overflow-auto px-0"
        ItemsClass="space-y-0"
        ItemClass="py-1.5 px-0 border-b border-gray-100 rounded-none bg-transparent shadow-none">
    <Header>
        <div class="grid grid-cols-[40px_1.2fr_80px_1.2fr] gap-2 px-3 py-2 text-xs font-medium text-gray-500 border-b border-gray-200 bg-gray-50">
            <span class="text-center">序号</span>
            <span>名称</span>
            <span class="text-center">大小</span>
            <span>备注</span>
        </div>
    </Header>
    <ItemTemplate>
        <div class="grid grid-cols-[40px_1.2fr_80px_1.2fr] gap-2 px-3 py-2 text-sm items-center">
            <span class="inline-flex items-center justify-center h-[20px] rounded bg-gray-100 text-gray-600 text-xs shrink-0 text-center">{{ idx + 1 }}</span>
            <a v-if="item.url" :href="item.url" target="_blank" rel="noopener"
                class="truncate text-blue-600 hover:text-blue-700 underline-offset-2 hover:underline font-medium"
                :title="item.fileName ?? item.name">
                {{ item.fileName ?? item.name }}
            </a>
            <span v-else class="truncate text-gray-800" :title="item.fileName ?? item.name">{{ item.fileName ?? item.name }}</span>
            <span class="text-center text-xs text-gray-500">{{ item.fileSizeText ?? item.fileSize }}</span>
            <span class="truncate text-gray-700" :title="item.remark">{{ item.remark }}</span>
        </div>
    </ItemTemplate>
    <Footer>
        <EleButton Type="Primary" Look="Text" Handler="ShowAtts" EnabledFor="Item.Id">管理</EleButton>
    </Footer>
</EleList>
```
