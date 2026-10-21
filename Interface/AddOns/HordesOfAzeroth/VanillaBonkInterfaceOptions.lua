local UI = VanillaBonkUI

local WIDTH = 980
local PAD = 14
local BOX_GAP = 16
local BOX_WIDTH = WIDTH - PAD * 2
local HALF_WIDTH = (BOX_WIDTH - BOX_GAP) / 2
local ROW = 25
local INDENT = 16
local SECTION_GAP = 24

local OWN_OPTIONS = {
    { key = "enemyPlates", label = "Enemy Nameplates", default = true,
        tooltip = "Shows health bars over enemies near you.", apply = function() UI.ApplyEnemyPlates() end },
    { key = "damageMeter", label = "Damage Meter", default = true,
        tooltip = "Shows how much damage each of your spells has done this run.", apply = function() UI.ApplyDamageMeter() end },
    { key = "incomingNumbers", label = "Damage Taken Numbers", default = true,
        tooltip = "Shows the damage you take as numbers beside your hero." },
    { key = "healthNumbers", label = "Health Numbers", default = true,
        tooltip = "Shows your health as numbers on your health bars.", apply = function() UI.ApplyHealthNumbers() end },
}

local healthBar = { pending = nil }

local SECTION_ROWS = {
    {
        {
            name = "Hordes of Azeroth",
            height = 3 * ROW + 20,
            columns = 3,
            own = { { 1, 1, 0 }, { 2, 1, 1 }, { 3, 1, 2 }, { 4, 2, 2 } },
            healthBar = { 2, 0, -22 },
        },
    },
    {
        {
            name = "Controls",
            height = 88,
            columns = 2,
            sliders = { { 1, 1, 0 } },
            checks = { { 1, 2, 0 }, { 6, 1, 2 } },
            dropdowns = { { "UIOptionsFrameClickCameraDropDown", 2, 1 } },
        },
        {
            name = "Camera",
            height = 6 * ROW + 20,
            columns = 2,
            dropdowns = { { "UIOptionsFrameCameraDropDown", 1, 0 } },
            checks = { { 24, 1, 2 }, { 26, 1, 3 }, { 27, 1, 4 }, { 15, 1, 5 } },
            sliders = { { 2, 2, 0 }, { 3, 2, 2 }, { 4, 2, 4 } },
        },
    },
    {
        {
            name = "Display",
            height = 6 * ROW + 20,
            columns = 2,
            checks = {
                { 12, 1, 0 }, { 39, 1, 1 },
                { 21, 1, 2 }, { 22, 1, 3, INDENT }, { 23, 1, 4, INDENT },
                { 30, 2, 0 }, { 67, 2, 1 }, { 31, 2, 2 }, { 20, 2, 3 },
            },
        },
        {
            name = "Floating Combat Text",
            height = 6 * ROW + 20,
            columns = 2,
            checks = {
                { 52, 1, 0 }, { 53, 1, 3 }, { 54, 1, 4 }, { 55, 1, 5 },
                { 56, 2, 0 }, { 57, 2, 1 }, { 58, 2, 2 },
                { 19, 2, 3 }, { 11, 2, 4, INDENT }, { 9, 2, 5, INDENT },
            },
            dropdowns = { { "UIOptionsFrameCombatTextDropDown", 1, 1 } },
        },
    },
}

local frame = UIOptionsFrame
local built = false

local function HideParchment()
    local regions = { frame:GetRegions() }
    for _, region in ipairs(regions) do
        if region:GetObjectType() == "Texture" then
            region:Hide()
        end
    end
    for _, name in ipairs({ "BasicOptions", "AdvancedOptions", "UIOptionsFrameTab1", "UIOptionsFrameTab2" }) do
        local child = getglobal(name)
        if child then
            child:Hide()
            child.Show = child.Hide
        end
    end
end

local function DressFrame()
    frame:SetParent(UIParent)
    frame:ClearAllPoints()
    frame:SetPoint("CENTER", UIParent, "CENTER", 0, 0)
    frame:SetWidth(WIDTH)
    frame:SetFrameStrata("DIALOG")
    frame:SetBackdrop({
        bgFile = "Interface\\DialogFrame\\UI-DialogBox-Background",
        edgeFile = "Interface\\DialogFrame\\UI-DialogBox-Border",
        tile = true, tileSize = 32, edgeSize = 32,
        insets = { left = 11, right = 12, top = 12, bottom = 11 },
    })
    local header = frame:CreateTexture(nil, "ARTWORK")
    header:SetTexture("Interface\\DialogFrame\\UI-DialogBox-Header")
    header:SetWidth(300)
    header:SetHeight(64)
    header:SetPoint("TOP", frame, "TOP", 0, 12)
    UIOptionsFrameTitle:SetParent(frame)
    UIOptionsFrameTitle:SetDrawLayer("OVERLAY")
    UIOptionsFrameTitle:ClearAllPoints()
    UIOptionsFrameTitle:SetPoint("TOP", header, "TOP", 0, -14)
end

local function Place(widget, box, column, row, xOffset, yOffset)
    widget:SetParent(box)
    widget:ClearAllPoints()
    widget:SetPoint("TOPLEFT", box, "TOPLEFT", 10 + (column - 1) * box.columnWidth + (xOffset or 0), -8 - row * ROW + (yOffset or 0))
    widget:Show()
end

local function HealthBarLabel(value)
    for _, mode in ipairs(UI.HEALTH_BAR_MODES) do
        if mode.value == value then
            return mode.label
        end
    end
end

local function SelectHealthBar(value)
    healthBar.pending = value
    UI.ApplyHealthBarMode(value)
    UIDropDownMenu_SetSelectedValue(healthBar.dropdown, value)
    UIDropDownMenu_SetText(HealthBarLabel(value), healthBar.dropdown)
end

local function BuildHealthBarDropdown(box)
    local dropdown = CreateFrame("Frame", "VanillaBonkOptionsHealthBar", box, "UIDropDownMenuTemplate")
    local label = dropdown:CreateFontString(nil, "BACKGROUND", "GameFontNormalSmall")
    label:SetPoint("BOTTOMLEFT", dropdown, "TOPLEFT", 16, 3)
    label:SetText("Your Health Bar")
    UIDropDownMenu_SetWidth(110, dropdown)
    UIDropDownMenu_Initialize(dropdown, function()
        for _, mode in ipairs(UI.HEALTH_BAR_MODES) do
            local info = {}
            info.text = mode.label
            info.value = mode.value
            info.func = function()
                SelectHealthBar(this.value)
            end
            info.checked = healthBar.pending == mode.value
            UIDropDownMenu_AddButton(info)
        end
    end)
    return dropdown
end

local function BuildSection(section, width, height, x, y)
    local box = CreateFrame("Frame", "VanillaBonkOptions" .. gsub(section.name, " ", ""), frame, "OptionFrameBoxTemplate")
    box:SetWidth(width)
    box:SetHeight(height)
    box.columnWidth = (width - 20) / section.columns
    box:SetPoint("TOPLEFT", frame, "TOPLEFT", x, y)
    getglobal(box:GetName() .. "Title"):SetText(section.name)
    for _, check in ipairs(section.checks or {}) do
        Place(getglobal("UIOptionsFrameCheckButton" .. check[1]), box, check[2], check[3], check[4])
    end
    for _, own in ipairs(section.own or {}) do
        local option = OWN_OPTIONS[own[1]]
        local check = CreateFrame("CheckButton", "VanillaBonkOptionsCheck" .. own[1], box, "OptionsCheckButtonTemplate")
        check:SetWidth(26)
        check:SetHeight(26)
        getglobal(check:GetName() .. "Text"):SetText(option.label)
        check.tooltipText = option.tooltip
        option.check = check
        Place(check, box, own[2], own[3])
    end
    if section.healthBar then
        healthBar.dropdown = BuildHealthBarDropdown(box)
        Place(healthBar.dropdown, box, section.healthBar[1], section.healthBar[2], -16, section.healthBar[3] or -14)
    end
    for _, slider in ipairs(section.sliders or {}) do
        Place(getglobal("UIOptionsFrameSlider" .. slider[1]), box, slider[2], slider[3], 14, -16)
    end
    for _, dropdown in ipairs(section.dropdowns or {}) do
        Place(getglobal(dropdown[1]), box, dropdown[2], dropdown[3], -16, -14)
    end
    return box
end

local function FixCloseButton()
    local children = { frame:GetChildren() }
    for _, child in ipairs(children) do
        if child:GetObjectType() == "Button" and not child:GetName() and child.GetNormalTexture and child:GetNormalTexture() and
            strfind(child:GetNormalTexture():GetTexture() or "", "MinimizeButton") then
            child:ClearAllPoints()
            child:SetPoint("TOPRIGHT", frame, "TOPRIGHT", -4, -4)
        end
    end
end

local function BuildButtons(last)
    local reset = CreateFrame("Button", "VanillaBonkOptionsResetTutorial", frame, "OptionsButtonTemplate")
    reset:SetWidth(130)
    reset:SetText("Reset Tutorial")
    reset:SetPoint("TOPLEFT", last, "BOTTOMLEFT", 0, -12)
    reset:SetScript("OnClick", function()
        PlaySound("igMainMenuOptionCheckBoxOn")
        if UI.ResetTutorial then
            UI.ResetTutorial()
        end
    end)
    reset:SetScript("OnEnter", function()
        GameTooltip:SetOwner(this, "ANCHOR_RIGHT")
        GameTooltip:SetText("Shows the Hordes of Azeroth tips again from the start.", 1, 1, 1, 1, 1)
    end)
    reset:SetScript("OnLeave", function()
        GameTooltip:Hide()
    end)

    UIOptionsFrameCancel:ClearAllPoints()
    UIOptionsFrameCancel:SetPoint("BOTTOMRIGHT", frame, "BOTTOMRIGHT", -PAD - 2, 16)
    UIOptionsFrameOkay:ClearAllPoints()
    UIOptionsFrameOkay:SetPoint("RIGHT", UIOptionsFrameCancel, "LEFT", -4, 0)
    UIOptionsFrameDefaults:ClearAllPoints()
    UIOptionsFrameDefaults:SetPoint("BOTTOMLEFT", frame, "BOTTOMLEFT", PAD + 2, 16)
    for _, button in ipairs({ UIOptionsFrameCancel, UIOptionsFrameOkay, UIOptionsFrameDefaults }) do
        button:SetFrameLevel(frame:GetFrameLevel() + 2)
    end
    return reset
end

local function Build()
    if built then
        return
    end
    built = true
    HideParchment()
    DressFrame()
    FixCloseButton()
    local last
    local y = -46
    for _, row in ipairs(SECTION_ROWS) do
        local rowHeight = 0
        for _, section in ipairs(row) do
            rowHeight = math.max(rowHeight, section.height)
        end
        local width = table.getn(row) > 1 and HALF_WIDTH or BOX_WIDTH
        for index, section in ipairs(row) do
            local box = BuildSection(section, width, rowHeight, PAD + (index - 1) * (HALF_WIDTH + BOX_GAP), y)
            if index == 1 then
                last = box
            end
        end
        y = y - rowHeight - SECTION_GAP
    end
    local reset = BuildButtons(last)
    frame:SetHeight(-y - SECTION_GAP + 12 + reset:GetHeight() + 54)
end

local function LoadOwnOptions()
    if healthBar.dropdown then
        SelectHealthBar(UI.HealthBarMode())
    end
    for _, option in ipairs(OWN_OPTIONS) do
        if option.check then
            if option.get then
                option.check:SetChecked(option.get())
            else
                option.check:SetChecked(UI.Option(option.key, option.default))
            end
        end
    end
end

local BaseSave = UIOptionsFrame_Save
function UIOptionsFrame_Save()
    BaseSave()
    if healthBar.pending then
        UI.SetOption("healthBar", healthBar.pending)
        UI.ApplyHealthBarMode()
    end
    for _, option in ipairs(OWN_OPTIONS) do
        if option.check then
            local on = option.check:GetChecked() and true or false
            if option.set then
                option.set(on)
            else
                UI.SetOption(option.key, on)
            end
            if option.apply then
                option.apply()
            end
        end
    end
end

local BaseDefaults = UIOptionsFrame_SetDefaults
function UIOptionsFrame_SetDefaults()
    BaseDefaults()
    if healthBar.dropdown then
        SelectHealthBar("plate")
    end
    for _, option in ipairs(OWN_OPTIONS) do
        if option.check then
            option.check:SetChecked(option.default)
        end
    end
end

local BaseOnHide = frame:GetScript("OnHide")
frame:SetScript("OnHide", function()
    UI.ApplyHealthBarMode()
    if BaseOnHide then
        BaseOnHide()
    end
end)

local BaseOnShow = frame:GetScript("OnShow")
Build()
frame:SetScript("OnShow", function()
    if BaseOnShow then
        BaseOnShow()
    end
    this:SetScale(1)
    LoadOwnOptions()
    for _, button in ipairs({ UIOptionsFrameCancel, UIOptionsFrameOkay, UIOptionsFrameDefaults }) do
        button:SetFrameLevel(this:GetFrameLevel() + 2)
    end
end)
