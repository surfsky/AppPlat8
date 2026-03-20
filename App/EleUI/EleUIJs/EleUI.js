import "./App.EleUI.EleUIJs.EleManager.js";
import { Utils } from "./App.EleUI.EleUIJs.Utils.js";
import { DrawerHelper } from "./App.EleUI.EleUIJs.DrawerHelper.js";
import { EleTable } from "./App.EleUI.EleUIJs.EleTable.js";
import { EleForm } from "./App.EleUI.EleUIJs.EleForm.js";
import { EleTableAppBuilder } from "./App.EleUI.EleUIJs.EleTableAppBuilder.js";
import { EleFormAppBuilder } from "./App.EleUI.EleUIJs.EleFormAppBuilder.js";
import { EleAppBuilder } from "./App.EleUI.EleUIJs.EleAppBuilder.js";

const eleFixesCssHref = "/res/App.EleUI.EleUIJs.EleFixes.css";
if (!document.querySelector(`link[href="${eleFixesCssHref}"]`)) {
    const link = document.createElement("link");
    link.rel = "stylesheet";
    link.href = eleFixesCssHref;
    document.head.appendChild(link);
}

// Expose classes globally to keep compatibility with existing inline scripts.
window.EleTable = window.EleTable || EleTable;
window.EleForm = window.EleForm || EleForm;
window.Utils = window.Utils || Utils;
window.DrawerHelper = window.DrawerHelper || DrawerHelper;
window.EleTableAppBuilder = window.EleTableAppBuilder || EleTableAppBuilder;
window.EleFormAppBuilder = window.EleFormAppBuilder || EleFormAppBuilder;
window.EleAppBuilder = window.EleAppBuilder || EleAppBuilder;
