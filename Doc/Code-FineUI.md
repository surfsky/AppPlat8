#--------------------------------------------
# FineUICore Resource
#--------------------------------------------
FineUIPro：https://pro.fineui.com/
FineUIMvc：https://mvc.fineui.com/
FineUICore：https://core.fineui.com/
FineUICore （Razor Pages & Tag Helpers）：https://pages.fineui.com/
F.js：https://js.fineui.com/
Icon: https://pages.fineui.com/#/Config/Icons


#--------------------------------------------
# TIP
#--------------------------------------------
使用Panel，再套form，可以让页面卷动时工具栏始终置顶




#--------------------------------------------
# FineUI 配置
#--------------------------------------------
service.AddFineUI(Configuration);
app.UseFineUI();

public static class FineUIServiceExtensions
{
    public static IServiceCollection AddFineUI(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        ConfigSection section = config.GetSection("FineUI").Get<ConfigSection>();
        GlobalConfig.Configure(section);
        return services.AddSingleton((IFineUIService)new FineUIService());
    }
}
public static class FineUIMiddlewareExtensions
{
    public static IApplicationBuilder UseFineUI(this IApplicationBuilder builder)
    {
        PageContext.Configure(builder.ApplicationServices);
        builder.MapWhen((Func<HttpContext, bool>)((HttpContext P_0) => P_0.Request.Path.ToString().EndsWith("res.axd")), (Action<IApplicationBuilder>)delegate(IApplicationBuilder P_0)
        {
            P_0.UseFineUIHandler();
        });
        return builder.UseMiddleware<FineUIMiddleware>(Array.Empty<object>());
    }
}




#--------------------------------------------
# FineUICore RazorPage
#--------------------------------------------
PageContext (ajax交互方法1，手工写脚本传递到客户端执行）
    PageContext.RegisterStartupScript(ActiveWindow.GetHidePostBackReference());
    PageContext.RegisterStartupScript(String.Format("$('#mylabel').html('{0}')", "这是修改后的值！" + DateTime.Now.ToLongTimeString()));
    PageContext.RegisterStartupScript("top.window.location.reload(false);");
    PageContext.RegisterStartupScript("F.ui.Label1.el.attr('style','');");

UIHelper（ajax交互方法2，已经写好的脚本辅助方法）
    FineUI通过UIHelper，将服务器端的变更传递给客户端， 进行页面元素的动态变更
    UIHelper.Label("Label1").CssStyle(style);
    UIHelper.Window("Window1").Hidden(true);
    UIHelper.Button("btnClientClick2").ClientClick(Alert.GetShowInTopReference("客户端事件已改变！"));
    UIHelper.TabStrip("TabStrip1").ActiveTabIndex(nextIndex);
    UIHelper.GroupPanel("GroupPanel2").Collapsed(!collapsed);
    UIHelper.ToolbarText("ToolbarText1").Text(String.Format("工具条文本一（{0}）", DateTime.Now.ToLongTimeString()));
    UIHelper.ToolbarText("ToolbarText1").Hidden(!textHidden);
    UIHelper.ToolbarSeparator("ToolbarSeparator1").Hidden(!separatorHidden);
    UIHelper.Panel("Panel1").IconFont(IconFont.None);
    UIHelper.TextBox("TextBox2").Text(text1).Focus(true);
    UIHelper.Grid("Grid1").Title(title).ConfigColumns(columns, configOptions).DataSource(dataSource);


ActiveWindow
    ActiveWindow.HidePostBack();

Url
    Url.Handler("btnSaveClose_Click")
    http://localhost:52221/Admin/UserEdit?handler=btnSaveClose_Click
    public IActionResult OnPostbtnSaveClose_Click(){...}
    public Task<IActionResult> OnPostbtnSaveClose_ClickAsync(){...}

Alert
    Alert.ShowInTop("删除失败！需要先清空属于此部门的用户！");
    var js = Alert.GetShowInTopReference("通过ViewBag传递的客户端事件");


ViewBag
    ViewBag.btnClientClick2Script = Alert.GetShowInTopReference("通过ViewBag传递的客户端事件");

回传事件处理
    <f:Button ID="btnClose" OnClick="@Url.Handler("btnClose_Click")" Text="关闭窗体"></f:Button>
    public IActionResult OnPostBtnClose_Click()
    {
        UIHelper.Window("Window1").Hidden(true);
        return UIHelper.Result();
    }

回传整个表单
    public IActionResult OnPostBtnLogin_Click(IFormCollection values)
    {
        if (values["tbxUserName"] == "admin" && values["tbxPassword"] == "admin")
            ShowNotify("成功登录！", MessageBoxIcon.Success);
        else
            ShowNotify("用户名或密码错误！", MessageBoxIcon.Error);
        return UIHelper.Result();
    }

回传表单控件值
    指明了回传的控件为Form2，服务器端就可以获取该控件的子属性
        <f:Button ID="Button1"  ValidateForms="Form2" ValidateTarget="Top" Text="验证表单并提交" OnClick="@Url.Handler("Button1_Click")" OnClickFields="Form2"/>
        public IActionResult OnPostButton1_Click(string tbxUserName, string ddlGender_text)
        {
            ShowNotify("用户名：" + tbxUserName + "  性别：" + ddlGender_text);
            return UIHelper.Result();
        }
    指明回传控件为DropDownList1
        <f:DropDownList ID="DropDownList1" ..../>
        <f:Button ID="btnGetSelection" Text="获取下拉选中项" OnClick="@Url.Handler("btnGetSelection_Click")" OnClickFields="DropDownList1" />
        public IActionResult OnPostBtnGetSelection_Click(string DropDownList1, string DropDownList1_text)
        {
            UIHelper.Label("labResult").Text(String.Format("选中项：{0}（值：{1}）", DropDownList1_text, DropDownList1));
            return UIHelper.Result();
        }

数据校验
    //Model.Invalidate();
    UIHelper.TextBox("tbxOldPassword").MarkInvalid("当前密码不正确！");


数据绑定
    <f:DropDownList ID="DropDownList1" DataSource="@ViewBag.DropDownList1DataSource" />
    private void LoadData()
    {
        List<string> strList = new List<string>();
        strList.Add("可选项1");
        strList.Add("可选项2");
        strList.Add("可选项3");
        ViewBag.DropDownList1DataSource = strList;
    }

动态创建
    <f:Form BodyPadding="10" ID="Form2" IsFluid="true" CssClass="blockpanel" EnableCollapse="false" Title="表单" LabelWidth="120" LabelAlign="Top" >
        <Rows>
            <f:FormRow Items="@ViewBag.DynamicItems" />
        </Rows>
    </f:Form>
    public class FormDynamicModel : BaseModel
    {
        public void OnGet()
        {
            InitFormRows();
        }

        private void InitFormRows()
        {
            List<Field> fields = new List<Field>();
            var tbxUser = new TextBox();
            tbxUser.ID = "tbxUserName";
            tbxUser.Text = "";
            tbxUser.Label = "用户名";
            tbxUser.ShowLabel = true;
            tbxUser.ShowRedStar = true;
            tbxUser.Required = true;
            tbxUser.EmptyText = "请输入用户名";
            fields.Add(tbxUser);

            var ddlGender = new FineUICore.DropDownList();
            ddlGender.ID = "ddlGender";
            ddlGender.Label = "性别（回发事件）";
            ddlGender.Items.Add("男", "0");
            ddlGender.Items.Add("女", "1");
            ddlGender.AutoSelectFirstItem = false;
            // 添加后台事件处理函数
            ddlGender.Events.Add(new Event("change", Url.Handler("ddlGender_SelectedIndexChanged"), "ddlGender"));
            fields.Add(ddlGender);

            ViewBag.DynamicItems = fields.ToArray();
        }
        public IActionResult OnPostDdlGender_SelectedIndexChanged(string ddlGender_text)
        {
            ShowNotify("选择的性别：" + ddlGender_text);
            return UIHelper.Result();
        }
        public IActionResult OnPostButton1_Click(string tbxUserName, string ddlGender_text)
        {
            ShowNotify("用户名：" + tbxUserName + "  性别：" + ddlGender_text);
            return UIHelper.Result();
        }
    }

代码及标签方式混用
    标签法
        <f:SimpleForm BodyPadding="5px" Layout="VBox"   >
            <Items>
                <f:Button ID="btnSignOut" Text="注销" OnClick="@Url.Action("SignOut")"></f:Button>
                <f:Button ID="btnServerClick" Text="服务器端事件" OnClick="@Url.Handler("btnServerClick_Click")"></f:Button>
            </Items>
        </f:SimpleForm>
    函数法
        // 这是两种不同的方案，函数使用 HtmlHelper Extension，标签使用 TagHelper
        // 注意Items后面的括号要放上面，否则编辑器认不出来，无法自动显示后面的扩展方法。这是一个BUG。
        var F = Html.F();
        @F.SimpleForm().BodyPadding(5).Layout(LayoutType.VBox).Items(
            F.Button().EnableDefaultCorner(false).EnableDefaultState(false).IconFont(IconFont._Home).IconAlign(IconAlign.Top).OnClientClick("window.open('http://fineui.com/mvc/','_blank');"),
            F.Button().Text("SignOut").OnClick(Url.Handler("btnServerClick_Click"))
        );
    其它示例
        @(F.Panel().ID("Panel1").BodyPadding(5).ShowBorder(false).Layout(LayoutType.VBox).ShowHeader(false).Title("部门管理").IsViewPort(true)
            .Items(
                F.Grid().ID("Grid1").BoxFlex(1).ShowBorder(true).ShowHeader(false).EnableCheckBoxSelect(false).DataIDField("ID").DataTextField("Name").EnableSimulateTree(true)
                    .Columns(
                        F.RowNumberField().EnablePagingNumber(true),
                        F.RenderFieldFor(m => m.Name, true).DataSimulateTreeLevelField("TreeLevel").Width(180),
                        F.RenderFieldFor(m => m.Remark, true).ExpandUnusedSpace(true),
                        F.RenderFieldFor(m => m.SortIndex).Width(80),
                        F.RenderField().HeaderText("").EnableHeaderMenu(false).EnableColumnHide(false).RendererFunction("renderActionEdit").Width(50),
                        F.RenderField().HeaderText("").EnableHeaderMenu(false).EnableColumnHide(false).RendererFunction("renderActionDelete").Width(50)
                    )
                    .Toolbars(
                        F.Toolbar().ID("Toolbar1").Position(ToolbarPosition.Top)
                            .Items(
                                F.ToolbarFill(),
                                F.Button().ID("btnNew").Icon(Icon.Add).Text("新增部门").Listener("click", "onNewClick").Enabled(ViewBag.CoreDeptNew)
                            )
                    ).DataSource(Model)
                )
        )
        @(F.Window().ID("Window1").IsModal(true).Hidden(true).Target(Target.Top).EnableResize(true).EnableMaximize(true).EnableIFrame(true).Width(900).Height(550).OnClose(Url.Action("Dept_DoPostBack"), "Panel1"))



#--------------------------------------------
# Controls
#--------------------------------------------
HyperLink
    <f:HyperLink NavigateUrl="" Text="<img src='/res/images/empty.png' />" EncodeText="false" />

参数
    <f:FileUpload ID="filePhoto" ShowEmptyLabel="true" ButtonText="上传图片" ButtonOnly="true" ButtonIcon="ImageAdd" OnFileSelected="@Url.Handler("filePhoto_FileSelected")" OnFileSelectedFields="filePhoto,grid" />
    public IActionResult OnPostFilePhoto_FileSelected(IFormFile filePhoto, string[] grid_fields)
    {
        UIHelper.Grid("grid").DataSource(this.Results, grid_fields);
    }

Button
    触发及事件
        <f:Button Type="Submit" ID="btnLogin" Text="登录" ValidateTarget="Top" ValidateForms="SimpleForm1" OnClickFields="SimpleForm1" OnClick="@Url.Handler("btnLogin_Click")" />
        <f:Button ID="btnSubmit" ValidateForms="SimpleForm1" Text="提交" OnClickDelegate="@(e =>
            {
                e.Action = Url.Handler("btnSubmit_Click");
                e.Fields = "SimpleForm1";
                e.AjaxLoadingType = AjaxLoadingType.Mask;
                e.ShowAjaxLoadingMaskText = true;
                e.AjaxLoadingMaskText = "正在导入数据，请稍后...";
            })" 
            />
     js事件
        <f:Button ID="btnHomePage" EnableDefaultCorner="false" EnableDefaultState="false" IconFont="_Home" IconAlign="Top" ToolTip="官网首页" CssClass="icononlyaction" OnClientClick="window.open('http://pages.fineui.com/','_blank');"></f:Button>
        <f:Button ID="btnClose" Icon="SystemClose" Text="关闭" OnClientClick="F.activeWindow.hide();" />
        <f:Button ID="btnDeleteSelected" Icon="Delete" Text="移除" Enabled="@Model.PowerCoreTitleUserDelete" OnClientClick="onDeleteSelectedClick" />
        <f:Button ID="btnNew" Icon="Add" Text="新增部门" Enabled="@Model.PowerCoreDeptNew" OnClientClick="onNewClick()" />
        或
        <f:Button ID="btnNew" Icon="Add" Text="新增部门" Enabled="@Model.PowerCoreDeptNew">
            <Listeners>
                <f:Listener Event="click" Handler="onNewClick"></f:Listener>
            </Listeners>
        </f:Button>



TwinTriggerBox
    这两个方法都可以
        <f:TwinTriggerBox ID="tbSelectedRole" EnableEdit="false"
            Trigger1Icon="Clear" Trigger2Icon="Search" ShowTrigger1="false" ShowTrigger2="true" Label="所属角色"
            OnClientTrigger1Click="onSelectedRoleTrigger1Click();"
            OnClientTrigger2Click="onSelectedRoleTrigger2Click();"
            />
        <f:TwinTriggerBox ID="ttbSearchMessage" ShowLabel="false" EmptyText="搜索" Trigger1Icon="Clear" Trigger2Icon="Search" ShowTrigger1="false">
            <Listeners>
                <f:Listener Event="trigger1click" Handler="RoleUser_Grid2_DoPostBack('trigger1');"></f:Listener>
                <f:Listener Event="trigger2click" Handler="RoleUser_Grid2_DoPostBack('trigger2');"></f:Listener>
            </Listeners>
        </f:TwinTriggerBox>



Window
    <f:Window Width="350" WindowPosition="GoldenSection" EnableClose="false" IsModal="false" Title="登录表单" ID="Window1">
        <Items>
            <f:SimpleForm ShowHeader="false" BodyPadding="10" ShowBorder="false" ID="SimpleForm1">
                <Toolbars>
                    <f:Toolbar Position="Bottom" ToolbarAlign="Right" ID="Toolbar1">
                        <Items>
                            <f:Button Type="Submit" ID="btnLogin" Text="登录" ValidateTarget="Top" ValidateForms="SimpleForm1"
                                      OnClick="@Url.Handler("btnLogin_Click")" OnClickFields="SimpleForm1"></f:Button>
                            <f:Button Type="Reset" Text="重置" ID="btnReset"></f:Button>
                        </Items>
                    </f:Toolbar>
                </Toolbars>
                <Items>
                    <f:TextBox ShowRedStar="true" Required="true" Label="用户名" ID="tbxUserName"></f:TextBox>
                    <f:TextBox ShowRedStar="true" Required="true" TextMode="Password" Label="密码" ID="tbxPassword"></f:TextBox>
                </Items>
            </f:SimpleForm>
        </Items>
    </f:Window>
    <f:Window ID="Window1" IsModal="true" Hidden="true" Target="Top" EnableResize="true" EnableMaximize="true" EnableIFrame="true" Width="900" Height="550" >
        <Listeners>
            <f:Listener Event="close" Handler="DeptUser_Grid2_DoPostBack();"></f:Listener>
        </Listeners>
    </f:Window>


FileUpload
    单文件
        <f:FileUpload ID="filePhoto" ShowRedStar="false" ShowEmptyLabel="true" ButtonText="上传个人头像" ButtonOnly="true" Required="false" ButtonIcon="ImageAdd" OnFileSelected="@Url.Handler("filePhoto_FileSelected")" OnFileSelectedFields="filePhoto" />
        public IActionResult OnPostFilePhoto_FileSelected(IFormFile filePhoto, IFormCollection values)
        {
            if (filePhoto != null)
            {
                string fileName = filePhoto.FileName;
                if (!ValidateFileType(fileName))
                {
                    UIHelper.FileUpload("filePhoto").Reset();
                    ShowNotify("无效的文件类型！");
                }
                else
                {
                    fileName = fileName.Replace(":", "_").Replace(" ", "_").Replace("\\", "_").Replace("/", "_");
                    fileName = DateTime.Now.Ticks.ToString() + "_" + fileName;
                    using (var stream = new FileStream(FineUICore.PageContext.MapWebPath("~/upload/" + fileName), FileMode.Create))
                    {
                        filePhoto.CopyTo(stream);
                    }

                    UIHelper.Image("imgPhoto").ImageUrl("~/upload/" + fileName);
                    UIHelper.FileUpload("filePhoto").Reset();
                }
            }

            return UIHelper.Result();
        }
    多文件
        <f:FileUpload ID="filePhotos" EmptyText="请选择多张照片" Label="个人照片" Required="true" ButtonIcon="Add" ShowRedStar="true" Multiple="true" />
        <f:Button ID="btnSubmit" ValidateForms="SimpleForm1" Text="提交" OnClick="@Url.Handler("btnSubmit_Click")" OnClickFields="SimpleForm1" />
        public IActionResult OnPostBtnSubmit_Click(List<IFormFile> filePhotos, IFormCollection values)
        {
            var msg = new List<string>();
            foreach (IFormFile filePhoto in filePhotos)
            {
                string fileName = filePhoto.FileName;
                if (ValidateFileType(fileName))
                {
                    fileName = fileName.Replace(":", "_").Replace(" ", "_").Replace("\\", "_").Replace("/", "_");
                    fileName = DateTime.Now.Ticks.ToString() + "_" + fileName;
                    using (var stream = new FileStream(FineUICore.PageContext.MapWebPath("~/upload/" + fileName), FileMode.Create))
                    {
                        filePhoto.CopyTo(stream);
                    }

                    msg.Add("<div>路径：" + filePhoto.FileName + "</div>" +
                        "<div>照片：<br /><img src=\"" + Url.Content("~/upload/" + fileName) + "\" /></div>");
                }
            }

            UIHelper.Label("labResult").Text("<ol><li>" + string.Join("</li><li>", msg) + "</li></ol>", encodeText: false);
            UIHelper.SimpleForm("SimpleForm1").Reset();
            return UIHelper.Result();
        }



Grid
    <f:Grid ID="Grid1" IsFluid="true" CssClass="blockpanel" ShowBorder="true" ShowHeader="true" Title="表格" DataIDField="Id" DataTextField="Name" DataSource="@DataSourceUtil.GetDataTable()">
        <Columns>
            <f:RowNumberField />
            <f:RenderField HeaderText="姓名" DataField="Name" Width="100" />
            <f:RenderField HeaderText="性别" DataField="Gender" FieldType="Int" RendererFunction="renderGender" Width="80" />
            <f:RenderField HeaderText="入学年份" DataField="EntranceYear" FieldType="Int" Width="100" />
            <f:RenderCheckField HeaderText="是否在校" DataField="AtSchool" RenderAsStaticField="true" Width="100" />
            <f:RenderField HeaderText="所学专业" DataField="Major" RendererFunction="renderMajor" ExpandUnusedSpace="true" MinWidth="150" />
            <f:RenderField HeaderText="分组" DataField="Group" RendererFunction="renderGroup" Width="80" />
            <f:RenderField HeaderText="注册日期" DataField="LogTime" FieldType="Date" Renderer="Date" RendererArgument="yyyy-MM-dd" Width="100" />
        </Columns>
    </f:Grid>

Grid.Paging
    <f:Grid ID="Grid1" IsFluid="true" CssClass="blockpanel" ShowBorder="true" ShowHeader="true" Title="表格" DataIDField="Id" DataTextField="Name" EnableCheckBoxSelect="true" AllowPaging="true" PageSize="5" IsDatabasePaging="true"
            RecordCount="@Model.DataCount" DataSource="@Model.DataSource" ShowPagingMessage="false" OnPageIndexChanged="@Url.Handler("Grid1_PageIndexChanged")" OnPageIndexChangedFields="Grid1">
        <Columns>
            <f:RowNumberField />
            <f:RenderField HeaderText="姓名" DataField="Name" Width="100" />
            <f:RenderField HeaderText="性别" DataField="Gender" FieldType="Int" RendererFunction="renderGender" Width="80" />
            <f:RenderField HeaderText="入学年份" DataField="EntranceYear" FieldType="Int" Width="100" />
            <f:RenderCheckField HeaderText="是否在校" DataField="AtSchool" RenderAsStaticField="true" Width="100" />
            <f:RenderField HeaderText="所学专业" DataField="Major" RendererFunction="renderMajor" ExpandUnusedSpace="true" MinWidth="150" />
            <f:RenderField HeaderText="分组" DataField="Group" RendererFunction="renderGroup" Width="80" />
            <f:RenderField HeaderText="注册日期" DataField="LogTime" FieldType="Date" Renderer="Date" RendererArgument="yyyy-MM-dd" Width="100" />
        </Columns>
    </f:Grid>
    public class DatabaseModel : BaseModel
    {
        /// <summary>记录数</summary>
        public int DataCount { get; set; }
        public DataTable DataSource { get; set; }
        public void OnGet()
        {
            // 1.设置总项数（特别注意：数据库分页初始化时，一定要设置总记录数RecordCount）
            // 2.获取当前分页数据
            RecordCount = DataSourceUtil.GetTotalCount(); ;
            DataSource = DataSourceUtil.GetPagedDataTable(pageIndex: 0, pageSize: 5, DataCount);
        }
        public IActionResult OnPostGrid1_PageIndexChanged(string[] Grid1_fields, int Grid1_pageIndex)
        {
            var grid1 = UIHelper.Grid("Grid1");
            var recordCount = DataSourceUtil.GetTotalCount();

            // 1.设置总项数（数据库分页回发时，如果总记录数不变，可以不设置RecordCount）
            grid1.RecordCount(recordCount);

            // 2.获取当前分页数据
            var dataSource = DataSourceUtil.GetPagedDataTable(pageIndex: Grid1_pageIndex, pageSize: 5, recordCount: recordCount);
            grid1.DataSource(dataSource, Grid1_fields);
            return UIHelper.Result();
        }
    }

Image
    <f:Image For="CurrentUser.Photo" ID="imgPhoto" CssClass="photo" ImageUrl="~/res/images/blank.png" ShowEmptyLabel="true" Label="头像"  />









#--------------------------------------------
# Client listener 客户端脚本
#--------------------------------------------
MenuButton.onclientclick
    <f:MenuButton ID="btnEnableUsers" Text="启用">
        <Listeners>
            <f:Listener Event="click" Handler="onEnableUsersClick"></f:Listener>
        </Listeners>
    </f:MenuButton>

Grid.sorting/paging
    <Listeners>
        <f:Listener Event="sorting" Handler="TitleUser_Grid2_DoPostBack();"></f:Listener>
        <f:Listener Event="paging" Handler="TitleUser_Grid2_DoPostBack();"></f:Listener>
    </Listeners>

DropDownList.onchange
    <f:DropDownList ID="ddlGridPageSize" Width="80" SelectedValue="@Grid2PagingInfo.PageSize.ToString()" >
        <Listeners>
            <f:Listener Event="change" Handler="TitleUser_Grid2_DoPostBack('changeGridPageSize');" />
        </Listeners>
    </f:DropDownList>







#--------------------------------------------
# FineUI.js
#--------------------------------------------
F
    F.activeWindow.hideRefresh();
    F.activeWindow.close();
    F.activeWindow.hideExecuteScript('parent.removeActiveTab();');
    F.ready(function () {
        var iconList = F.ui.rblIconList;
        var tbxIcon = F.ui.Menu_ImageUrl;
        iconList.on('change', function () {
            tbxIcon.setValue(iconList.getValue());
        });
        tbxIcon.on('change', function () {
            iconList.setValue(tbxIcon.getValue());
        });
    });

loading
    F.ui.SimpleForm1.showLoading();
    F.ui.SimpleForm1.hodeLoading();


button
    F.create({
        type: 'button', id: 'btn2', renderTo: '#wrap1', text: '禁用的按钮', disabled: true,
        handler: function (event) {
            showNotify('你点击了刚刚启用的按钮');
        }
    });


confirm
    F.confirm({
        message: '确定删除此记录？',
        target: '_top',
        ok: function () {
            // 触发后台事件
            F.doPostBack('@Url.Action("Dept_DoPostBack")', 'Panel1', {
                actionType: 'delete',
                deletedRowID: rowData.id
            });
        }
    });

notify 
    F.notify({  
        message: "xxxx",
        target: "_top",
        messageIcon: "information"
    });


grid
    F.ui.Grid1.loadData([
        [1,"系统管理",0,"","","~/res/icon/cog.png","顶级菜单",10],
        [3,"用户",1,"~/Admin/UserList","CoreUserView","~/res/icon/tag_blue.png","二级菜单",10],
        [5,"职称用户",1,"~/Admin/TitleUser","CoreTitleUserView","~/res/icon/tag_blue.png","二级菜单",30],
        [7,"部门用户",1,"~/Admin/DeptUser","CoreDeptUserView","~/res/icon/tag_blue.png","二级菜单",50],
        [9,"角色用户",1,"~/Admin/RoleUser","CoreRoleUserView","~/res/icon/tag_blue.png","二级菜单",70],
        [11,"角色权限",1,"~/Admin/RolePower","CoreRolePowerView","~/res/icon/tag_blue.png","二级菜单",90],
        [12,"菜单",1,"~/Admin/Menu","CoreMenuView","~/res/icon/tag_blue.png","二级菜单",100],
        [13,"在线统计",1,"~/Admin/Online","CoreOnlineView","~/res/icon/tag_blue.png","二级菜单",110],
        [14,"系统配置",1,"~/Admin/Config","CoreConfigView","~/res/icon/tag_blue.png","二级菜单",120],
        [15,"修改密码",1,"~/Admin/ChangePassword","","~/res/icon/tag_blue.png","二级菜单",130],
        [2,"测试菜单",0,"","","~/res/icon/folder.png","顶级菜单",20],
        [19,"按钮5",1,"~/Test/ButtonClick","","~/res/icon/tag_yellow.png","",3],
        [18,"测试页面1",1,"~/Test/FormDynamic","","~/res/icon/tag_yellow.png","三级菜单",10],
        [17,"测试页面2",1,"~/Test/GridPaging","","~/res/icon/page.png","二级菜单",20]
    ]);
    F.ui.Grid1.clearSelection();


post
    F.doPostBack('@Url.Handler("Title_DoPostBack")', 'Panel1', {
        actionType: 'delete',
        deletedRowID: rowData.id
    });


window
    F.ui.Window1.show('@Url.Content("~/Admin/DeptNew")', '新增');
    F.ui.Window1.bodyEl.html($('#source' + nextSourceNumber).html());
    F.ui.Window1.showLoading(0.8);
    F.ui.Window1.hideLoading();

