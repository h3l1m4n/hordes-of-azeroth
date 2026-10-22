local BORDERLESS_LABEL = "Borderless"
local BORDERLESS_TOOLTIP = "Check to fill your screen with the game window, without a title bar or borders. Looks like fullscreen, and Alt+Tab switches instantly."

local button = OptionsFrameCheckButton17
local buttonText = OptionsFrameCheckButton17Text
local stockLoad = OptionsFrame_Load
local stockSave = OptionsFrame_Save

local function HasBorderless()
    return HoAWindow_GetBorderless ~= nil and HoAWindow_SetBorderless ~= nil
end

local function TurnOffStockMaximize()
    if GetCVar("gxMaximize") ~= "0" then
        SetCVar("gxMaximize", "0")
    end
end

local function ShowBorderless()
    buttonText:SetText(BORDERLESS_LABEL)
    button.tooltipText = BORDERLESS_TOOLTIP
    button:SetChecked(HoAWindow_GetBorderless())
    if OptionsFrameCheckButton10:GetChecked() then
        OptionsFrame_EnableCheckBox(button)
    else
        OptionsFrame_DisableCheckBox(button)
    end
end

OptionsFrame_Load = function()
    stockLoad()
    if HasBorderless() then
        ShowBorderless()
    end
end

OptionsFrame_Save = function()
    if not HasBorderless() then
        stockSave()
        return
    end
    local borderless = button:GetChecked() and true or false
    button:SetChecked(nil)
    stockSave()
    TurnOffStockMaximize()
    button:SetChecked(borderless)
    HoAWindow_SetBorderless(borderless, GetCVar("gxWindow") == "1")
end

local guard = CreateFrame("Frame")
guard:RegisterEvent("PLAYER_LOGIN")
guard:SetScript("OnEvent", TurnOffStockMaximize)
