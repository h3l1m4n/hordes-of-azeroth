local UI = VanillaBonkUI

local MAX_WIDTH = 304
local MIN_WIDTH = 220
local HERO_BAR_HALF = 372
local EDGE = 6
local TAB_HEIGHT = 24
local TAB_SIDE = 12
local ROW = 19
local ROW_GAP = 3
local PAD = 4
local MAX_ROWS = 10
local MAX_PARTS = 4
local BAR_TEXTURE = "Interface\\TargetingFrame\\UI-StatusBar"
local TAB_TEXTURE = "Interface\\ChatFrame\\ChatFrameTab"
local BAR_SPEED = 8
local MODE_SPELLS, MODE_TYPES = 1, 2

local function Short(value)
    if value >= 1000000 then
        return format("%.2fM", value / 1000000)
    elseif value >= 10000 then
        return format("%.1fK", value / 1000)
    elseif value >= 1000 then
        return format("%.2fK", value / 1000)
    end
    return format("%d", floor(value))
end

local function Percent(part, total)
    return total > 0 and floor(part * 100 / total + 0.5) or 0
end

local function ColorCode(kind)
    return format("|cff%02x%02x%02x", kind.r * 255, kind.g * 255, kind.b * 255)
end

local meter = CreateFrame("Frame", "VanillaBonkMeter", UIParent)
meter:SetPoint("BOTTOMRIGHT", UIParent, "BOTTOMRIGHT", -EDGE, EDGE)
meter:SetWidth(MAX_WIDTH)
meter:SetHeight(TAB_HEIGHT + PAD * 2 + ROW)
meter:SetFrameStrata("LOW")
meter:Hide()

meter.back = meter:CreateTexture(nil, "BACKGROUND")
meter.back:SetTexture(UI.FLAT)
meter.back:SetVertexColor(0, 0, 0, 0.42)
meter.back:SetPoint("TOPLEFT", meter, "TOPLEFT", 0, -(TAB_HEIGHT - 2))
meter.back:SetPoint("BOTTOMRIGHT", meter, "BOTTOMRIGHT", 0, 0)

meter.total = UI.Text(meter, 12, UI.NUMBER_FONT)
meter.total:SetPoint("TOPRIGHT", meter, "TOPRIGHT", -4, -6)
meter.total:SetJustifyH("RIGHT")

local mode = MODE_SPELLS
local spells = {}
local seconds = 0
local rows = {}
local tabs = {}
local Refresh

local function TabPiece(tab, left, right)
    local piece = tab:CreateTexture(nil, "BACKGROUND")
    piece:SetTexture(TAB_TEXTURE)
    piece:SetTexCoord(left, right, 0, 1)
    piece:SetHeight(TAB_HEIGHT)
    return piece
end

local function ShowTabs()
    for _, tab in ipairs(tabs) do
        local active = tab.mode == mode
        tab:SetAlpha(active and 1 or 0.6)
        if active then
            tab.text:SetTextColor(1, 1, 1)
        else
            tab.text:SetTextColor(1, 0.82, 0)
        end
    end
end

local function Tab(label, tabMode)
    local tab = CreateFrame("Button", nil, meter)
    tab:SetHeight(TAB_HEIGHT)
    tab.mode = tabMode
    tab.left = TabPiece(tab, 0, 0.25)
    tab.left:SetWidth(TAB_SIDE)
    tab.left:SetPoint("TOPLEFT", tab, "TOPLEFT", 0, 0)
    tab.right = TabPiece(tab, 0.75, 1)
    tab.right:SetWidth(TAB_SIDE)
    tab.right:SetPoint("TOPRIGHT", tab, "TOPRIGHT", 0, 0)
    tab.middle = TabPiece(tab, 0.25, 0.75)
    tab.middle:SetPoint("TOPLEFT", tab.left, "TOPRIGHT", 0, 0)
    tab.middle:SetPoint("TOPRIGHT", tab.right, "TOPLEFT", 0, 0)
    tab.text = UI.Text(tab, 11)
    tab.text:SetPoint("CENTER", tab, "CENTER", 0, -1)
    tab.text:SetText(label)
    tab:SetWidth(floor(tab.text:GetStringWidth() + 2 * TAB_SIDE + 6))
    tab:SetScript("OnClick", function()
        mode = this.mode
        VanillaBonkMeterMode = mode
        PlaySound("igCharacterInfoTab")
        ShowTabs()
        Refresh()
    end)
    local previous = tabs[getn(tabs)]
    if previous then
        tab:SetPoint("LEFT", previous, "RIGHT", -2, 0)
    else
        tab:SetPoint("TOPLEFT", meter, "TOPLEFT", 0, 0)
    end
    tinsert(tabs, tab)
    return tab
end

Tab("Spells", MODE_SPELLS)
Tab("Damage types", MODE_TYPES)

local function AddPartLines(entry)
    for _, part in ipairs(entry.parts) do
        local kind = UI.DamageType(part.key)
        GameTooltip:AddDoubleLine(kind.name, Short(part.damage) .. " (" .. Percent(part.damage, entry.damage) .. "%)",
            kind.r, kind.g, kind.b, 1, 1, 1)
    end
end

local function SpellTooltip(entry, share)
    GameTooltip:AddLine(entry.name, 1, 1, 1)
    GameTooltip:AddDoubleLine("Damage", Short(entry.damage), 1, 0.82, 0, 1, 1, 1)
    GameTooltip:AddDoubleLine("Per second", Short(seconds > 0 and entry.damage / seconds or 0), 1, 0.82, 0, 1, 1, 1)
    GameTooltip:AddDoubleLine("Share", share .. "%", 1, 0.82, 0, 1, 1, 1)
    GameTooltip:AddDoubleLine("Kills", entry.kills or 0, 1, 0.82, 0, 1, 1, 1)
    if getn(entry.parts) > 1 then
        AddPartLines(entry)
    end
end

local function TypeTooltip(entry, share)
    local kind = UI.DamageType(entry.key)
    GameTooltip:AddDoubleLine(kind.name, Short(entry.damage) .. " (" .. share .. "%)", kind.r, kind.g, kind.b, 1, 1, 1)
    if kind.note then
        GameTooltip:AddLine(kind.note, 0.75, 0.75, 0.75)
    end
    for _, source in ipairs(entry.sources) do
        GameTooltip:AddDoubleLine(source.name, Short(source.damage), 1, 1, 1, 1, 1, 1)
    end
end

local function ShowRowTooltip()
    local entry = this.entry
    if not entry then
        return
    end
    this.track:SetVertexColor(1, 1, 1, 0.14)
    GameTooltip:SetOwner(this, "ANCHOR_LEFT")
    if entry.sources then
        TypeTooltip(entry, this.share or 0)
    else
        SpellTooltip(entry, this.share or 0)
    end
    GameTooltip:Show()
end

local function HideRowTooltip()
    this.track:SetVertexColor(1, 1, 1, 0.06)
    GameTooltip:Hide()
end

for index = 1, MAX_ROWS do
    local row = CreateFrame("Frame", nil, meter)
    local top = -(TAB_HEIGHT + PAD + (index - 1) * (ROW + ROW_GAP))
    row:SetHeight(ROW)
    row:SetPoint("TOPLEFT", meter, "TOPLEFT", PAD, top)
    row:SetPoint("TOPRIGHT", meter, "TOPRIGHT", -PAD, top)
    row:EnableMouse(true)
    row:SetScript("OnEnter", ShowRowTooltip)
    row:SetScript("OnLeave", HideRowTooltip)

    row.icon = row:CreateTexture(nil, "ARTWORK")
    row.icon:SetWidth(ROW)
    row.icon:SetHeight(ROW)
    row.icon:SetPoint("LEFT", row, "LEFT", 0, 0)
    row.icon:SetTexCoord(0.07, 0.93, 0.07, 0.93)

    row.track = row:CreateTexture(nil, "BACKGROUND")
    row.track:SetTexture(UI.FLAT)
    row.track:SetVertexColor(1, 1, 1, 0.06)
    row.track:SetPoint("TOPLEFT", row.icon, "TOPRIGHT", 2, 0)
    row.track:SetPoint("BOTTOMRIGHT", row, "BOTTOMRIGHT", 0, 0)

    row.parts = {}
    for part = 1, MAX_PARTS do
        local fill = row:CreateTexture(nil, "BORDER")
        fill:SetTexture(BAR_TEXTURE)
        fill:SetHeight(ROW)
        fill:Hide()
        row.parts[part] = fill
    end

    row.value = UI.Text(row, 12, UI.NUMBER_FONT)
    row.value:SetPoint("RIGHT", row, "RIGHT", -4, 0)
    row.value:SetJustifyH("RIGHT")
    row.name = UI.Text(row, 11)
    row.name:SetPoint("LEFT", row.track, "LEFT", 4, 0)
    row.name:SetPoint("RIGHT", row.value, "LEFT", -4, 0)
    row.name:SetJustifyH("LEFT")

    row.shown = 0
    row.target = 0
    row:Hide()
    rows[index] = row
end

local function DrawRow(row)
    local entry = row.entry
    local width = row.barWidth * row.shown
    local x = 0
    for index, fill in ipairs(row.parts) do
        local part = entry and entry.parts[index]
        local partWidth = (part and entry.damage > 0) and width * part.damage / entry.damage or 0
        if partWidth >= 0.5 then
            local kind = UI.DamageType(part.key)
            fill:SetVertexColor(kind.r, kind.g, kind.b)
            fill:SetWidth(partWidth)
            fill:ClearAllPoints()
            fill:SetPoint("TOPLEFT", row.track, "TOPLEFT", x, 0)
            fill:Show()
            x = x + partWidth
        else
            fill:Hide()
        end
    end
end

local function SpellEntries()
    local entries = {}
    for _, spell in pairs(spells) do
        if spell.icon then
            tinsert(entries, {
                id = "s" .. spell.index, index = spell.index, icon = spell.icon, name = spell.name or "", damage = spell.damage,
                kills = spell.kills, parts = UI.DamageParts(spell.types, spell.damage, spell.icon),
            })
        end
    end
    return entries
end

local function TypeEntries()
    local byKey = {}
    local entries = {}
    for _, spell in pairs(spells) do
        if spell.icon then
            for _, part in ipairs(UI.DamageParts(spell.types, spell.damage, spell.icon)) do
                local entry = byKey[part.key]
                if not entry then
                    local kind = UI.DamageType(part.key)
                    entry = { id = "t" .. part.key, key = part.key, icon = kind.icon, name = kind.name, damage = 0, sources = {}, index = 0 }
                    byKey[part.key] = entry
                    tinsert(entries, entry)
                end
                entry.damage = entry.damage + part.damage
                tinsert(entry.sources, { name = spell.name or "", damage = part.damage })
            end
        end
    end
    for _, entry in ipairs(entries) do
        entry.parts = { { key = entry.key, damage = entry.damage } }
        table.sort(entry.sources, function(a, b) return a.damage > b.damage end)
    end
    return entries
end

local function MeterWidth()
    local room = floor(UIParent:GetWidth() / 2 - HERO_BAR_HALF - EDGE * 2)
    return max(MIN_WIDTH, min(MAX_WIDTH, room))
end

Refresh = function()
    local width = MeterWidth()
    meter:SetWidth(width)
    local entries = mode == MODE_TYPES and TypeEntries() or SpellEntries()
    local total, top = 0, 0
    for _, entry in ipairs(entries) do
        total = total + entry.damage
        top = max(top, entry.damage)
    end
    table.sort(entries, function(a, b)
        if a.damage ~= b.damage then
            return a.damage > b.damage
        end
        return a.index < b.index
    end)

    local count = min(getn(entries), MAX_ROWS)
    for index, row in ipairs(rows) do
        local entry = entries[index]
        if index <= count and entry then
            row.entry = entry
            row.share = Percent(entry.damage, total)
            row.target = top > 0 and entry.damage / top or 0
            row.barWidth = width - PAD * 2 - ROW - 2
            if row.key ~= entry.id then
                row.key = entry.id
                row.shown = row.target
            end
            row.icon:SetTexture(UI.ICON_PATH .. entry.icon)
            row.name:SetText(entry.name)
            row.value:SetText(Short(entry.damage) .. " |cffa0a0a0" .. row.share .. "%|r")
            DrawRow(row)
            row:Show()
        else
            row.entry = nil
            row.key = nil
            row:Hide()
        end
    end
    meter:SetHeight(TAB_HEIGHT + PAD * 2 + max(1, count) * (ROW + ROW_GAP) - ROW_GAP)
    meter.total:SetText(Short(total) .. " |cffa0a0a0(" .. Short(seconds > 0 and total / seconds or 0) .. "/s)|r")
end

meter:SetScript("OnUpdate", function()
    local step = min(1, arg1 * BAR_SPEED)
    for _, row in ipairs(rows) do
        if row:IsShown() and row.shown ~= row.target then
            local shown = row.shown + (row.target - row.shown) * step
            if abs(shown - row.target) < 0.002 then
                shown = row.target
            end
            row.shown = shown
            DrawRow(row)
        end
    end
end)

local events = CreateFrame("Frame")
events:RegisterEvent("VARIABLES_LOADED")
events:SetScript("OnEvent", function()
    if VanillaBonkMeterMode == MODE_TYPES then
        mode = MODE_TYPES
    end
    ShowTabs()
end)
ShowTabs()

local handlers = {}

local function Spell(index)
    if not spells[index] then
        spells[index] = { index = index, damage = 0, kills = 0 }
    end
    return spells[index]
end

local inRun = false

local function ShowMeter()
    inRun = true
    if UI.Option("damageMeter", true) then
        meter:Show()
    else
        meter:Hide()
    end
end

function UI.ApplyDamageMeter()
    if inRun then
        ShowMeter()
    end
end

handlers["start"] = function()
    spells = {}
    seconds = 0
    for _, row in ipairs(rows) do
        row.key = nil
    end
    Refresh()
    ShowMeter()
end

handlers["end"] = function()
    inRun = false
    meter:Hide()
end

handlers["state"] = function(f)
    seconds = tonumber(f[14] or "") or seconds
    if f[13] then
        for index, item in ipairs(UI.Split(f[13], ",")) do
            local parts = UI.Split(item, ":")
            local spell = Spell(index)
            spell.damage = tonumber(parts[1]) or 0
            spell.kills = tonumber(parts[2]) or 0
        end
    end
    if f[15] then
        for index, item in ipairs(UI.Split(f[15], ",")) do
            Spell(index).types = item
        end
    end
    Refresh()
    ShowMeter()
end

handlers["loadout"] = function(f)
    local index = 0
    for _, item in ipairs(UI.Split(f[2] or "", ",")) do
        local parts = UI.Split(item, ":")
        if parts[1] == "w" or parts[1] == "e" then
            index = index + 1
            local spell = Spell(index)
            if spell.icon and spell.icon ~= parts[2] then
                spell.damage = 0
                spell.kills = 0
                spell.types = nil
            end
            spell.icon = parts[2] ~= "" and parts[2] or "INV_Misc_QuestionMark"
        end
    end
    for extra = index + 1, MAX_ROWS do
        spells[extra] = nil
    end
    Refresh()
end

handlers["pw"] = function(f)
    local index = tonumber(f[2])
    if index and spells[index] then
        spells[index].name = f[3]
        Refresh()
    end
end

UI.Listen(handlers)
