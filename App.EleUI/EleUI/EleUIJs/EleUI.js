import "./EleManager.js";
import eleFixesCssText from "./EleFixes.css";
import { Utils } from "./Utils.js";
import { DrawerHelper } from "./DrawerHelper.js";
import { EleTable } from "./EleTable.js";
import { EleForm } from "./EleForm.js";
import { EleList } from "./EleList.js";
import { EleTableAppBuilder } from "./EleTableAppBuilder.js";
import { EleFormAppBuilder } from "./EleFormAppBuilder.js";
import { EleListAppBuilder } from "./EleListAppBuilder.js";
import { EleAppBuilder } from "./EleAppBuilder.js";

// Inject bundled CSS text so consumers only load one eleui.js file.
const styleId = "eleui-runtime-style";
if (eleFixesCssText && !document.getElementById(styleId)) {
    const style = document.createElement("style");
    style.id = styleId;
    style.textContent = eleFixesCssText;
    document.head.appendChild(style);
}

// Expose classes globally to keep compatibility with existing inline scripts.
window.EleTable = window.EleTable || EleTable;
window.EleForm = window.EleForm || EleForm;
window.EleList = window.EleList || EleList;
window.Utils = window.Utils || Utils;
window.DrawerHelper = window.DrawerHelper || DrawerHelper;
window.EleTableAppBuilder = window.EleTableAppBuilder || EleTableAppBuilder;
window.EleFormAppBuilder = window.EleFormAppBuilder || EleFormAppBuilder;
window.EleListAppBuilder = window.EleListAppBuilder || EleListAppBuilder;
window.EleAppBuilder = window.EleAppBuilder || EleAppBuilder;
