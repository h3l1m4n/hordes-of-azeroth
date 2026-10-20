local UI = VanillaBonkUI

local WIDTH = 450
local BOX_HEIGHT = 34
local BOX_INSET = 7
local BOX_EDGE = 20
local NAME_HEIGHT = 16
local TOP = -36
local SKULL_SIZE = 26
local AFFIX_SIZE = 24
local AFFIX_ART = 0.72
local AFFIX_GAP = 6
local SMOOTHING = 8
local SKULL = "Interface\\TargetingFrame\\UI-TargetingFrame-Skull"
local GOLD_BORDER = "Interface\\DialogFrame\\UI-DialogBox-Gold-Border"
local STONE_BORDER = "Interface\\DialogFrame\\UI-DialogBox-Border"

local LOOKS = {
    boss = { fill = { 0.78, 0.12, 0.12 }, name = { 1, 0.82, 0 }, tag = "|cffff4b4bSTAGE BOSS|r", kind = "Stage boss", border = GOLD_BORDER, edge = { 1, 1, 1 } },
    mini = { fill = { 0.9, 0.48, 0.1 }, name = { 1, 1, 1 }, tag = "|cffffa040MINI-BOSS|r", kind = "Mini-boss", border = STONE_BORDER, edge = { 0.85, 0.85, 0.85 } },
}

local function Short(value)
    if value >= 1000000 then
        return format("%.2fm", value / 1000000)
    elseif value >= 10000 then
        return format("%.1fk", value / 1000)
    end
    return tostring(floor(value))
end

local bar = CreateFrame("Frame", "VanillaBonkBossBar", UIParent)
bar:SetWidth(WIDTH)
bar:SetHeight(NAME_HEIGHT + BOX_HEIGHT)
bar:SetPoint("TOP", UIParent, "TOP", 0, TOP)
bar:SetFrameStrata("MEDIUM")
bar:Hide()

bar.name = UI.Text(bar, 13)
bar.tag = UI.Text(bar, 10)

bar.box = CreateFrame("Frame", nil, bar)
bar.box:SetPoint("TOPLEFT", bar, "TOPLEFT", 0, -NAME_HEIGHT)
bar.box:SetPoint("TOPRIGHT", bar, "TOPRIGHT", 0, -NAME_HEIGHT)
bar.box:SetHeight(BOX_HEIGHT)

bar.health = CreateFrame("StatusBar", nil, bar.box)
bar.health:SetPoint("TOPLEFT", bar.box, "TOPLEFT", BOX_INSET, -BOX_INSET)
bar.health:SetPoint("BOTTOMRIGHT", bar.box, "BOTTOMRIGHT", -BOX_INSET, BOX_INSET)
bar.health:SetStatusBarTexture("Interface\\TargetingFrame\\UI-StatusBar")
bar.health:SetMinMaxValues(0, 1)

bar.trail = bar.box:CreateTexture(nil, "ARTWORK")
bar.trail:SetTexture(UI.FLAT)
bar.trail:SetVertexColor(1, 0.85, 0.6, 0.6)
bar.trail:SetPoint("TOPLEFT", bar.health, "TOPLEFT", 0, 0)
bar.trail:SetHeight(BOX_HEIGHT - 2 * BOX_INSET)

bar.edge = CreateFrame("Frame", nil, bar.box)
bar.edge:SetAllPoints(bar.box)
bar.edge:SetFrameLevel(bar.health:GetFrameLevel() + 2)

bar.value = UI.Text(bar.edge, 12, UI.NUMBER_FONT)
bar.value:SetPoint("CENTER", bar.health, "CENTER", 0, 0)

for side = 1, 2 do
    local skull = bar.edge:CreateTexture(nil, "OVERLAY")
    skull:SetTexture(SKULL)
    skull:SetWidth(SKULL_SIZE)
    skull:SetHeight(SKULL_SIZE)
    if side == 1 then
        skull:SetPoint("RIGHT", bar.box, "LEFT", 4, 0)
    else
        skull:SetPoint("LEFT", bar.box, "RIGHT", -4, 0)
    end
end

bar.shown = 1
bar.trailShown = 1
bar.target = 1
bar.affixNames = {}

local function SetLook(look)
    if bar.look == look then
        return
    end
    bar.look = look
    bar.box:SetBackdrop({
        bgFile = UI.FLAT, edgeFile = look.border, edgeSize = BOX_EDGE,
        insets = { left = 5, right = 5, top = 5, bottom = 5 },
    })
    bar.box:SetBackdropColor(0, 0, 0, 0.85)
    bar.box:SetBackdropBorderColor(look.edge[1], look.edge[2], look.edge[3])
    bar.health:SetStatusBarColor(look.fill[1], look.fill[2], look.fill[3])
    bar.name:SetTextColor(look.name[1], look.name[2], look.name[3])
end

local function PlaceName()
    local gap = 8
    local nameWidth = bar.name:GetStringWidth()
    local total = nameWidth + gap + bar.tag:GetStringWidth()
    bar.name:ClearAllPoints()
    bar.name:SetPoint("TOPLEFT", bar, "TOP", -total / 2, 0)
    bar.tag:ClearAllPoints()
    bar.tag:SetPoint("LEFT", bar.name, "RIGHT", gap, 0)
end

local affixInfo = {}

bar.affixes = CreateFrame("Frame", "VanillaBonkBossAffixes", bar)
bar.affixes:SetHeight(AFFIX_SIZE)
bar.affixes:SetPoint("TOP", bar, "BOTTOM", 0, -6)
bar.affixes:Hide()
bar.affixes.icons = {}

local function AffixIcon(index)
    local icon = bar.affixes.icons[index]
    if icon then
        return icon
    end
    icon = UI.Ring(bar.affixes, AFFIX_SIZE)
    icon.art = UI.RingArt(icon, "")
    if not icon.art.SetMask then
        icon.art:SetWidth(AFFIX_SIZE * AFFIX_ART)
        icon.art:SetHeight(AFFIX_SIZE * AFFIX_ART)
    end
    icon:EnableMouse(true)
    icon:SetScript("OnEnter", function()
        local info = affixInfo[this.affix]
        GameTooltip:SetOwner(this, "ANCHOR_BOTTOM")
        GameTooltip:AddLine(this.affix, 1, 0.82, 0)
        if info and info.description ~= "" then
            GameTooltip:AddLine(info.description, 1, 1, 1, 1)
        end
        GameTooltip:Show()
    end)
    icon:SetScript("OnLeave", function() GameTooltip:Hide() end)
    bar.affixes.icons[index] = icon
    return icon
end

local function ShowAffixes(text)
    if text == bar.affixText then
        return
    end
    bar.affixText = text
    bar.affixNames = (text and text ~= "") and UI.Split(text, ",") or {}
    local count = getn(bar.affixNames)
    for index, name in ipairs(bar.affixNames) do
        local icon = AffixIcon(index)
        local info = affixInfo[name]
        icon.affix = name
        icon.art:SetTexture(UI.ICON_PATH .. (info and info.icon ~= "" and info.icon or "INV_Misc_QuestionMark"))
        icon:ClearAllPoints()
        icon:SetPoint("LEFT", bar.affixes, "LEFT", (index - 1) * (AFFIX_SIZE + AFFIX_GAP), 0)
        icon:Show()
    end
    for index = count + 1, getn(bar.affixes.icons) do
        bar.affixes.icons[index]:Hide()
    end
    if count > 0 then
        bar.affixes:SetWidth(count * AFFIX_SIZE + (count - 1) * AFFIX_GAP)
        bar.affixes:Show()
    else
        bar.affixes:Hide()
    end
end

bar:EnableMouse(true)
bar:SetScript("OnEnter", function()
    GameTooltip:SetOwner(this, "ANCHOR_BOTTOM")
    GameTooltip:AddDoubleLine(this.bossName or "", this.look and this.look.kind or "", 1, 0.82, 0, 1, 0.3, 0.3)
    GameTooltip:AddLine(Short(this.hp or 0) .. " / " .. Short(this.hpMax or 0) .. " health", 1, 1, 1)
    if getn(this.affixNames) > 0 then
        GameTooltip:AddLine(table.concat(this.affixNames, ", "), 1, 0.82, 0, 1)
    end
    GameTooltip:Show()
end)
bar:SetScript("OnLeave", function() GameTooltip:Hide() end)

bar:SetScript("OnUpdate", function()
    local step = min(1, arg1 * SMOOTHING)
    this.shown = this.shown + (this.target - this.shown) * step
    if this.trailShown > this.shown then
        this.trailShown = this.trailShown - max(0.002, (this.trailShown - this.shown) * arg1 * 2.5)
        if this.trailShown < this.shown then
            this.trailShown = this.shown
        end
    else
        this.trailShown = this.shown
    end
    this.health:SetValue(this.shown)
    this.trail:SetWidth(max(0.01, (WIDTH - 2 * BOX_INSET) * this.trailShown))
end)

local function ShowBoss(name, health, maxHealth, kind, affixes)
    local fraction = maxHealth > 0 and health / maxHealth or 0
    if not bar:IsShown() or bar.bossName ~= name then
        bar.shown, bar.trailShown = fraction, fraction
        bar.bossName = name
    end
    bar.target = fraction
    bar.hp, bar.hpMax = health, maxHealth

    local look = kind == "boss" and LOOKS.boss or LOOKS.mini
    SetLook(look)
    bar.name:SetText(name)
    bar.tag:SetText(look.tag)
    PlaceName()
    bar.value:SetText(floor(fraction * 100 + 0.5) .. "%  |cffc8c8c8" .. Short(health) .. " / " .. Short(maxHealth) .. "|r")
    ShowAffixes(affixes)
    bar:Show()
end

local function HideBoss()
    bar:Hide()
    bar.bossName = nil
    ShowAffixes(nil)
end

UI.Listen({
    ["boss"] = function(f)
        if f[2] and f[2] ~= "" then
            ShowBoss(f[2], tonumber(f[3]) or 0, tonumber(f[4]) or 0, f[5], f[6])
        else
            HideBoss()
        end
    end,
    ["affixinfo"] = function(f)
        if f[2] and f[2] ~= "" then
            affixInfo[f[2]] = { icon = f[3] or "", description = f[4] or "" }
        end
    end,
    ["start"] = HideBoss,
    ["end"] = HideBoss,
})
