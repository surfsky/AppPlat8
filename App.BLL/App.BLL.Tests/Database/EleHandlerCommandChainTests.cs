using System.Collections.Generic;
using App.EleUI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace App.BLL.Tests.Database;

[TestClass]
public class EleHandlerCommandChainTests
{
    [TestMethod]
    public void ShowNotify_Sleep_Redirect_BuildsOrderedJsonResult()
    {
        var chain = EleHandler.ShowNotify("保存成功", NotifyType.Success, "提示")
            .Sleep(100)
            .Redirect("/Checks/Config/CheckSheetForm?id=123&md=Edit")
            .ReloadData();

        Assert.IsInstanceOfType<IActionResult>(chain);
        var result = chain.ToResult();
        Assert.IsInstanceOfType<EleResult>(result.Value);
        var response = (EleResult)result.Value;
        var commandsProperty = response.Data.GetType().GetProperty("commands");
        Assert.IsNotNull(commandsProperty);
        var commands = (List<ClientCommand>)commandsProperty.GetValue(response.Data);
        var reloadDataProperty = response.Data.GetType().GetProperty("reloadData");

        Assert.AreEqual(3, commands.Count);
        Assert.IsNotNull(reloadDataProperty);
        Assert.IsTrue((bool)reloadDataProperty.GetValue(response.Data));
        Assert.AreEqual(ClientCommandType.Notify, commands[0].Command);
        Assert.AreEqual(ClientCommandType.Sleep, commands[1].Command);
        Assert.AreEqual(100, ((SleepArgs)commands[1].Args).Milliseconds);
        Assert.AreEqual(ClientCommandType.Redirect, commands[2].Command);
        Assert.AreEqual("/Checks/Config/CheckSheetForm?id=123&md=Edit", ((RedirectArgs)commands[2].Args).Url);
    }
}