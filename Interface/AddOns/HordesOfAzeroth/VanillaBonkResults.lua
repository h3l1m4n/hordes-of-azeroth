local UI = VanillaBonkUI

local PANEL_WIDTH = 720
local PAD_X = 30
local PAD_TOP = 24
local PAD_BOTTOM = 16
local LEFT_WIDTH = 196
local RIGHT_X = PAD_X + LEFT_WIDTH + 22
local RIGHT_WIDTH = PANEL_WIDTH - RIGHT_X - PAD_X
local STATS_TOP = 76
local STAT_ROW = 19
local HEADER_HEIGHT = 20
local ROW_HEIGHT = 18
local ROW_GAP = 3
local SUB_HEIGHT = 13
local SUB_GAP = 3
local SUB_INDENT = 18
local COLUMN_WIDTH = 44
local COLUMNS_WIDTH = 3 * COLUMN_WIDTH
local MAX_LINES = 12
local MAX_PARTS = 4
local TYPE_BAR_HEIGHT = 7
local LEGEND_LINE = 13
local FOOTER_HEIGHT = 24
local BUTTON_WIDTH = 110
local FADE = 0.3
local BAR_TEXTURE = "Interface\\TargetingFrame\\UI-StatusBar"
local TITLE_DEFEAT = { 1, 0.23, 0.15 }
local TITLE_VICTORY = { 1, 0.82, 0 }
local TITLE_HOME = { 0.35, 1, 0.35 }
local CLASS_ART = {
    WARRIOR = "WarriorArms", MAGE = "MageFire", HUNTER = "HunterMarksmanship", ROGUE = "RogueCombat", PRIEST = "PriestHoly",
    SHAMAN = "ShamanElementalCombat", DRUID = "DruidBalance", WARLOCK = "WarlockDestruction", PALADIN = "PaladinHoly",
}

local function Short(value)
    value = tonumber(value) or 0
    if value >= 1000000 then
        return format("%.2fm", value / 1000000)
    elseif value >= 10000 then
        return format("%.1fk", value / 1000)
    end
    return tostring(floor(value))
end

local function Thousands(value)
    local text = tostring(floor(tonumber(value) or 0))
    local result = ""
    while strlen(text) > 3 do
        result = "," .. strsub(text, -3) .. result
        text = strsub(text, 1, -4)
    end
    return text .. result
end

local function Percent(part, total)
    return total > 0 and floor(part * 100 / total + 0.5) or 0
end

local screen = CreateFrame("Frame", "VanillaBonkSummary", UIParent)
screen:SetAllPoints(UIParent)
screen:SetFrameStrata("DIALOG")
screen:EnableMouse(true)
screen:Hide()
tinsert(UISpecialFrames, "VanillaBonkSummary")

screen.dim = screen:CreateTexture(nil, "BACKGROUND")
screen.dim:SetTexture("Interface\\AddOns\\HordesOfAzeroth\\Textures\\OfferVignette")
screen.dim:SetPoint("TOPLEFT", WorldFrame, "TOPLEFT", -2, 2)
screen.dim:SetPoint("BOTTOMRIGHT", WorldFrame, "BOTTOMRIGHT", 2, -2)
screen.dim:SetVertexColor(0, 0, 0, 1)

screen.wound = screen:CreateTexture(nil, "BORDER")
screen.wound:SetTexture("Interface\\FullScreenTextures\\LowHealth")
screen.wound:SetBlendMode("ADD")
screen.wound:SetAllPoints(screen.dim)
screen.wound:SetAlpha(0.6)

local panel = CreateFrame("Frame", nil, screen)
screen.panel = panel
panel:SetWidth(PANEL_WIDTH)
panel:SetHeight(300)
panel:SetPoint("CENTER", screen, "CENTER", 0, 20)
panel:SetBackdrop({ edgeFile = "Interface\\DialogFrame\\UI-DialogBox-Border", edgeSize = 16 })

panel.art = panel:CreateTexture(nil, "BACKGROUND")
panel.art:SetPoint("TOPLEFT", panel, "TOPLEFT", 5, -5)
panel.art:SetPoint("BOTTOMRIGHT", panel, "BOTTOMRIGHT", -5, 5)
panel.shade = panel:CreateTexture(nil, "BORDER")
panel.shade:SetAllPoints(panel.art)
UI.Shade(panel.shade, 0.84, 0.52)

panel.title = panel:CreateFontString(nil, "OVERLAY")
panel.title:SetFont(UI.TITLE_FONT, 30, "OUTLINE")
panel.title:SetPoint("TOPLEFT", panel, "TOPLEFT", PAD_X, -PAD_TOP)
panel.title:SetJustifyH("LEFT")

panel.subtitle = UI.Text(panel, 11)
panel.subtitle:SetPoint("TOPLEFT", panel, "TOPLEFT", PAD_X, -PAD_TOP - 38)
panel.subtitle:SetWidth(LEFT_WIDTH)
panel.subtitle:SetJustifyH("LEFT")
panel.subtitle:SetTextColor(0.9, 0.83, 0.64)

panel.stats = {}
for index = 1, 7 do
    local stat = CreateFrame("Frame", nil, panel)
    stat:SetWidth(LEFT_WIDTH)
    stat:SetHeight(STAT_ROW - 2)
    stat:SetPoint("TOPLEFT", panel, "TOPLEFT", PAD_X, -PAD_TOP - STATS_TOP - (index - 1) * STAT_ROW)
    stat.icon = stat:CreateTexture(nil, "ARTWORK")
    stat.icon:SetWidth(13)
    stat.icon:SetHeight(13)
    stat.icon:SetPoint("LEFT", stat, "LEFT", 0, 0)
    stat.icon:SetTexCoord(0.07, 0.93, 0.07, 0.93)
    stat.label = UI.Text(stat, 11)
    stat.label:SetPoint("LEFT", stat.icon, "RIGHT", 7, 0)
    stat.label:SetTextColor(0.9, 0.83, 0.64)
    stat.value = UI.Text(stat, 13, UI.NUMBER_FONT)
    stat.value:SetPoint("RIGHT", stat, "RIGHT", 0, 0)
    stat.value:SetJustifyH("RIGHT")
    panel.stats[index] = stat
end

panel.divider = panel:CreateTexture(nil, "ARTWORK")
panel.divider:SetTexture(UI.FLAT)
panel.divider:SetWidth(1)
panel.divider:SetPoint("TOPLEFT", panel, "TOPLEFT", PAD_X + LEFT_WIDTH + 10, -PAD_TOP)
panel.divider:SetGradientAlpha("VERTICAL", 1, 0.82, 0, 0.5, 1, 0.82, 0, 0)
panel.dividerLow = panel:CreateTexture(nil, "ARTWORK")
panel.dividerLow:SetTexture(UI.FLAT)
panel.dividerLow:SetWidth(1)
panel.dividerLow:SetPoint("TOP", panel.divider, "BOTTOM", 0, 0)
panel.dividerLow:SetGradientAlpha("VERTICAL", 1, 0.82, 0, 0, 1, 0.82, 0, 0.5)

panel.heading = UI.Text(panel, 12)
panel.heading:SetPoint("TOPLEFT", panel, "TOPLEFT", RIGHT_X, -PAD_TOP)
panel.heading:SetTextColor(1, 0.82, 0)
panel.heading:SetText("Damage by spell")

local COLUMNS = { { "Damage", 2 * COLUMN_WIDTH }, { "Share", COLUMN_WIDTH }, { "Kills", 0 } }
for _, column in ipairs(COLUMNS) do
    local label = UI.Text(panel, 10)
    label:SetPoint("TOPRIGHT", panel, "TOPRIGHT", -PAD_X - column[2], -PAD_TOP - 1)
    label:SetWidth(COLUMN_WIDTH)
    label:SetJustifyH("RIGHT")
    label:SetTextColor(0.62, 0.62, 0.62)
    label:SetText(column[1])
end

local function ColumnText(parent, right)
    local text = UI.Text(parent, 13, UI.NUMBER_FONT)
    text:SetWidth(COLUMN_WIDTH)
    text:SetJustifyH("RIGHT")
    text.right = right
    return text
end


local function AddRunLines(entry)
    GameTooltip:AddLine(" ")
    GameTooltip:AddDoubleLine(Short(entry.damage) .. " damage", Thousands(entry.kills) .. " kills", 1, 1, 1, 0.6, 0.6, 0.6)
end

local function SpellLineTooltip(line, entry)
    if not (VanillaBonk_LoadoutTooltip and VanillaBonk_LoadoutTooltip(line, entry.name)) then
        GameTooltip:SetOwner(line, "ANCHOR_RIGHT")
        local r, g, b = 1, 1, 1
        if entry.evolved then
            r, g, b = 1, 0.5, 0
        end
        GameTooltip:AddDoubleLine(entry.name, entry.level > 0 and ("Rank " .. entry.level) or "", r, g, b, 0.6, 0.6, 0.6)
    end
    AddRunLines(entry)
    GameTooltip:Show()
end

local function EffectLineTooltip(line, entry)
    local kind = UI.DamageType(entry.school)
    GameTooltip:SetOwner(line, "ANCHOR_RIGHT")
    GameTooltip:AddLine(entry.name, kind.r, kind.g, kind.b)
    if kind.note then
        GameTooltip:AddLine(kind.note, 1, 0.82, 0, 1)
    end
    GameTooltip:AddLine("Applied by " .. entry.parent.name .. ".", 0.6, 0.6, 0.6, 1)
    AddRunLines(entry)
    GameTooltip:Show()
end

local function ShowLineTooltip(line)
    local entry = line.entry
    if not entry then
        return
    end
    line.track:SetVertexColor(0.25, 0.2, 0.05, 0.8)
    if entry.parent then
        EffectLineTooltip(line, entry)
    else
        SpellLineTooltip(line, entry)
    end
end

local function HideLineTooltip()
    this.track:SetVertexColor(0, 0, 0, 0.55)
    GameTooltip:Hide()
end

panel.lines = {}
for index = 1, MAX_LINES do
    local line = CreateFrame("Frame", nil, panel)
    line:SetWidth(RIGHT_WIDTH)
    line.icon = line:CreateTexture(nil, "ARTWORK")
    line.icon:SetTexCoord(0.07, 0.93, 0.07, 0.93)
    line.track = line:CreateTexture(nil, "BACKGROUND")
    line.track:SetTexture(UI.FLAT)
    line.track:SetVertexColor(0, 0, 0, 0.55)
    line.parts = {}
    for part = 1, MAX_PARTS do
        local fill = line:CreateTexture(nil, "BORDER")
        fill:SetTexture(BAR_TEXTURE)
        line.parts[part] = fill
    end
    line.name = UI.Text(line, 11)
    line.name:SetJustifyH("LEFT")
    line.damage = ColumnText(line, 2 * COLUMN_WIDTH)
    line.share = ColumnText(line, COLUMN_WIDTH)
    line.kills = ColumnText(line, 0)
    line:EnableMouse(true)
    line:SetScript("OnEnter", function() ShowLineTooltip(this) end)
    line:SetScript("OnLeave", HideLineTooltip)
    line:Hide()
    panel.lines[index] = line
end

panel.typeBar = CreateFrame("Frame", nil, panel)
panel.typeBar:SetWidth(RIGHT_WIDTH)
panel.typeBar:SetHeight(TYPE_BAR_HEIGHT)
panel.typeBar:SetBackdrop({ bgFile = UI.FLAT, edgeFile = UI.FLAT, edgeSize = 1 })
panel.typeBar:SetBackdropColor(0, 0, 0, 1)
panel.typeBar:SetBackdropBorderColor(0, 0, 0, 1)
panel.typeBar.parts = {}
panel.legend = {}

local function TypeSegment(index)
    local segment = panel.typeBar.parts[index]
    if not segment then
        segment = panel.typeBar:CreateTexture(nil, "ARTWORK")
        segment:SetTexture(BAR_TEXTURE)
        segment:SetHeight(TYPE_BAR_HEIGHT - 2)
        panel.typeBar.parts[index] = segment
    end
    return segment
end

local function LegendEntry(index)
    local entry = panel.legend[index]
    if not entry then
        entry = CreateFrame("Frame", nil, panel)
        entry:SetHeight(LEGEND_LINE)
        entry.swatch = entry:CreateTexture(nil, "ARTWORK")
        entry.swatch:SetTexture(UI.FLAT)
        entry.swatch:SetWidth(7)
        entry.swatch:SetHeight(7)
        entry.swatch:SetPoint("LEFT", entry, "LEFT", 0, 0)
        entry.text = UI.Text(entry, 10)
        entry.text:SetPoint("LEFT", entry.swatch, "RIGHT", 3, 0)
        entry.text:SetTextColor(0.85, 0.85, 0.85)
        panel.legend[index] = entry
    end
    return entry
end

panel.footerRule = panel:CreateTexture(nil, "ARTWORK")
panel.footerRule:SetTexture(UI.FLAT)
panel.footerRule:SetHeight(1)
panel.footerRule:SetWidth((PANEL_WIDTH - 2 * PAD_X) / 2)
panel.footerRule:SetGradientAlpha("HORIZONTAL", 1, 0.82, 0, 0, 1, 0.82, 0, 0.55)
panel.footerRuleRight = panel:CreateTexture(nil, "ARTWORK")
panel.footerRuleRight:SetTexture(UI.FLAT)
panel.footerRuleRight:SetHeight(1)
panel.footerRuleRight:SetWidth((PANEL_WIDTH - 2 * PAD_X) / 2)
panel.footerRuleRight:SetPoint("LEFT", panel.footerRule, "RIGHT", 0, 0)
panel.footerRuleRight:SetGradientAlpha("HORIZONTAL", 1, 0.82, 0, 0.55, 1, 0.82, 0, 0)

local build = CreateFrame("Frame", nil, panel)
panel.build = build
build:SetHeight(FOOTER_HEIGHT)
build.glyphLabel = UI.Text(build, 10)
build.glyphLabel:SetPoint("LEFT", build, "LEFT", 0, 0)
build.glyphLabel:SetTextColor(0.78, 0.72, 0.56)
build.glyphLabel:SetText("Glyphs")
build.glyphs = {}
for index = 1, 3 do
    local ring = UI.Ring(build, 20, true)
    ring.icon = UI.RingArt(ring, "")
    ring:EnableMouse(true)
    ring:SetScript("OnEnter", function()
        if VanillaBonk_GlyphTooltip then
            VanillaBonk_GlyphTooltip(this, this.glyphId)
        end
    end)
    ring:SetScript("OnLeave", function() GameTooltip:Hide() end)
    build.glyphs[index] = ring
end
build.passiveLabel = UI.Text(build, 10)
build.passiveLabel:SetTextColor(0.78, 0.72, 0.56)
build.passiveLabel:SetText("Passives")
build.passives = {}
for index = 1, 6 do
    local slot = CreateFrame("Frame", nil, build)
    slot:SetWidth(17)
    slot:SetHeight(17)
    slot.icon = slot:CreateTexture(nil, "ARTWORK")
    slot.icon:SetAllPoints(slot)
    slot.icon:SetTexCoord(0.07, 0.93, 0.07, 0.93)
    slot.index = index
    slot:EnableMouse(true)
    slot:SetScript("OnEnter", function()
        if VanillaBonk_PassiveTooltip then
            VanillaBonk_PassiveTooltip(this, this.index)
        end
    end)
    slot:SetScript("OnLeave", function() GameTooltip:Hide() end)
    build.passives[index] = slot
end

panel.earned = UI.Text(panel, 11)
panel.earned:SetJustifyH("CENTER")

panel.done = CreateFrame("Button", nil, panel, "UIPanelButtonTemplate")
panel.done:SetWidth(BUTTON_WIDTH)
panel.done:SetHeight(22)
panel.done:SetText("Continue")

local run = {}
local weapons = {}
local passives = {}

local function Close()
    if screen.closing or not screen:IsShown() then
        return
    end
    screen.closing = true
    PlaySound("igMainMenuOptionCheckBoxOn")
end

panel.done:SetScript("OnClick", Close)

screen:SetScript("OnUpdate", function()
    local want = this.closing and 0 or 1
    if this.alpha ~= want then
        local step = arg1 / FADE
        this.alpha = want > this.alpha and min(want, this.alpha + step) or max(want, this.alpha - step)
        this:SetAlpha(this.alpha)
        if this.alpha <= 0 then
            this:Hide()
        end
    end
end)

screen:SetScript("OnHide", function()
    this.closing = false
end)

local function WeaponParts(weapon)
    local parts = {}
    local dots = 0
    for _, sub in ipairs(weapon.subs) do
        dots = dots + sub.damage
    end
    local direct = weapon.damage - dots
    if direct > 0 or getn(weapon.subs) == 0 then
        tinsert(parts, { key = weapon.school, damage = max(0, direct) })
    end
    for _, sub in ipairs(weapon.subs) do
        if sub.damage > 0 then
            tinsert(parts, { key = sub.school, damage = sub.damage })
        end
    end
    return parts
end

local function DrawBar(line, x, width, fraction, parts, total)
    line.track:ClearAllPoints()
    line.track:SetPoint("TOPLEFT", line, "TOPLEFT", x, 0)
    line.track:SetWidth(max(1, width))
    line.track:SetHeight(line:GetHeight())
    local filled = 0
    for index, fill in ipairs(line.parts) do
        local part = parts[index]
        local partWidth = (part and total > 0) and width * fraction * part.damage / total or 0
        if partWidth >= 0.5 then
            local kind = UI.DamageType(part.key)
            fill:SetVertexColor(kind.r, kind.g, kind.b)
            fill:ClearAllPoints()
            fill:SetPoint("TOPLEFT", line.track, "TOPLEFT", filled, 0)
            fill:SetWidth(partWidth)
            fill:SetHeight(line:GetHeight())
            fill:Show()
            filled = filled + partWidth
        else
            fill:Hide()
        end
    end
end

local function FillLine(line, entry, top, total, y)
    local sub = entry.parent ~= nil
    local height = sub and SUB_HEIGHT or ROW_HEIGHT
    local indent = sub and SUB_INDENT or 0
    line.entry = entry
    line:SetHeight(height)
    line:ClearAllPoints()
    line:SetPoint("TOPLEFT", panel, "TOPLEFT", RIGHT_X, -y)
    line.icon:SetTexture(UI.ICON_PATH .. entry.icon)
    line.icon:SetWidth(height)
    line.icon:SetHeight(height)
    line.icon:ClearAllPoints()
    line.icon:SetPoint("LEFT", line, "LEFT", indent, 0)

    local barX = indent + height + 5
    local fullWidth = RIGHT_WIDTH - COLUMNS_WIDTH - ROW_HEIGHT - 11
    local barWidth = RIGHT_WIDTH - COLUMNS_WIDTH - barX - 6
    local parts = sub and { { key = entry.school, damage = entry.damage } } or WeaponParts(entry)
    DrawBar(line, barX, barWidth, top > 0 and min(1, entry.damage / top * fullWidth / barWidth) or 0, parts, entry.damage)

    line.name:ClearAllPoints()
    line.name:SetPoint("LEFT", line, "LEFT", barX + 5, 0)
    line.name:SetFont(UI.FONT, sub and 10 or 11)
    for _, text in ipairs({ line.damage, line.share, line.kills }) do
        text:SetFont(UI.NUMBER_FONT, sub and 11 or 13)
        text:ClearAllPoints()
        text:SetPoint("RIGHT", line, "RIGHT", -text.right, 0)
        text:SetHeight(height)
    end
    if sub then
        line.name:SetText("|cffc8c8c8" .. entry.name .. "|r")
        line.damage:SetTextColor(0.78, 0.78, 0.78)
        line.kills:SetTextColor(0.78, 0.78, 0.78)
    else
        local rank = entry.level > 0 and ("  |cffc8c8c8Rank " .. entry.level .. "|r") or ""
        local color = entry.evolved and "|cffff8000" or "|cffffffff"
        line.name:SetText(color .. entry.name .. "|r" .. rank)
        line.damage:SetTextColor(1, 1, 1)
        line.kills:SetTextColor(1, 1, 1)
    end
    line.share:SetTextColor(0.78, 0.78, 0.78)
    line.damage:SetText(Short(entry.damage))
    line.share:SetText(Percent(entry.damage, total) .. "%")
    line.kills:SetText(Thousands(entry.kills))
    line:Show()
    return height + (sub and SUB_GAP or ROW_GAP)
end

local function TypeTotals()
    local byKey, list = {}, {}
    for _, weapon in ipairs(weapons) do
        for _, part in ipairs(WeaponParts(weapon)) do
            if not byKey[part.key] then
                byKey[part.key] = { key = part.key, damage = 0 }
                tinsert(list, byKey[part.key])
            end
            byKey[part.key].damage = byKey[part.key].damage + part.damage
        end
    end
    table.sort(list, function(a, b) return a.damage > b.damage end)
    return list
end

local function FillTypes(y, total)
    local types = TypeTotals()
    panel.typeBar:ClearAllPoints()
    panel.typeBar:SetPoint("TOPLEFT", panel, "TOPLEFT", RIGHT_X, -y)
    local x = 1
    local width = RIGHT_WIDTH - 2
    for index, entry in ipairs(types) do
        local segment = TypeSegment(index)
        local kind = UI.DamageType(entry.key)
        local segmentWidth = total > 0 and width * entry.damage / total or 0
        segment:SetVertexColor(kind.r, kind.g, kind.b)
        segment:ClearAllPoints()
        segment:SetPoint("TOPLEFT", panel.typeBar, "TOPLEFT", x, -1)
        segment:SetWidth(max(0.5, segmentWidth))
        if segmentWidth >= 0.5 then
            segment:Show()
        else
            segment:Hide()
        end
        x = x + segmentWidth
    end
    for index = getn(types) + 1, getn(panel.typeBar.parts) do
        panel.typeBar.parts[index]:Hide()
    end

    local legendX, legendY = 0, y + TYPE_BAR_HEIGHT + 4
    for index, entry in ipairs(types) do
        local legend = LegendEntry(index)
        local kind = UI.DamageType(entry.key)
        legend.swatch:SetVertexColor(kind.r, kind.g, kind.b)
        legend.text:SetText(kind.name .. " " .. Percent(entry.damage, total) .. "%")
        local width = 10 + legend.text:GetStringWidth()
        if legendX > 0 and legendX + width > RIGHT_WIDTH then
            legendX = 0
            legendY = legendY + LEGEND_LINE
        end
        legend:SetWidth(width)
        legend:ClearAllPoints()
        legend:SetPoint("TOPLEFT", panel, "TOPLEFT", RIGHT_X + legendX, -legendY)
        legend:Show()
        legendX = legendX + width + 9
    end
    for index = getn(types) + 1, getn(panel.legend) do
        panel.legend[index]:Hide()
    end
    return legendY + LEGEND_LINE
end

local function FillBuild()
    local x = build.glyphLabel:GetStringWidth() + 6
    local taken = VanillaBonk_GlyphsTaken and VanillaBonk_GlyphsTaken() or {}
    local shown = 0
    for index, ring in ipairs(build.glyphs) do
        local glyph = VanillaBonkGlyphInfo and VanillaBonkGlyphInfo[taken[index]]
        if glyph then
            ring.icon:SetTexture(UI.ICON_PATH .. (glyph.icon ~= "" and glyph.icon or "INV_Misc_QuestionMark"))
            ring.glyphId = taken[index]
            ring:ClearAllPoints()
            ring:SetPoint("LEFT", build, "LEFT", x, 0)
            ring:Show()
            x = x + 23
            shown = shown + 1
        else
            ring:Hide()
        end
    end
    if shown == 0 then
        build.glyphLabel:Hide()
        x = 0
    else
        build.glyphLabel:Show()
        x = x + 10
    end
    build.passiveLabel:ClearAllPoints()
    build.passiveLabel:SetPoint("LEFT", build, "LEFT", x, 0)
    if getn(passives) > 0 then
        build.passiveLabel:Show()
        x = x + build.passiveLabel:GetStringWidth() + 6
    else
        build.passiveLabel:Hide()
    end
    for index, slot in ipairs(build.passives) do
        if passives[index] then
            slot.icon:SetTexture(UI.ICON_PATH .. passives[index])
            slot:ClearAllPoints()
            slot:SetPoint("LEFT", build, "LEFT", x, 0)
            slot:Show()
            x = x + 20
        else
            slot:Hide()
        end
    end
    build:SetWidth(max(1, x))
    return x
end

local function FillStats()
    local _, class = UnitClass("player")
    local stats = {
        { "INV_Misc_PocketWatch_01", "Time", UI.FormatTime(run.seconds) },
        { nil, "Level", run.level or "1" },
        { "INV_Misc_Bone_HumanSkull_01", "Kills", Thousands(run.kills) },
        { "Ability_Warrior_Challange", "Elites", Thousands(run.elites) },
        { "Ability_Rogue_Eviscerate", "Mini-bosses", Thousands(run.minis) },
        { "INV_Misc_Head_Dragon_01", "Stage bosses", Thousands(run.bosses) },
        { "INV_Shield_06", "Damage taken", "|cffff5a4a" .. Thousands(run.damageTaken) .. "|r" },
    }
    for index, stat in ipairs(panel.stats) do
        local data = stats[index]
        if data[1] then
            stat.icon:SetTexture(UI.ICON_PATH .. data[1])
            stat.icon:SetTexCoord(0.07, 0.93, 0.07, 0.93)
        else
            UI.SetRoundClassCrest(stat.icon, class)
        end
        stat.label:SetText(data[2])
        stat.value:SetText(data[3])
    end
    panel.art:SetTexture("Interface\\TalentFrame\\" .. (CLASS_ART[class or ""] or "MageArcane") .. "-TopLeft")
    UI.TalentArtCoords(panel.art, PANEL_WIDTH - 10, panel:GetHeight() - 10)
end

local function Place()
    if run.difficulty and run.difficulty ~= "" then
        return run.map .. " (" .. run.difficulty .. ")"
    end
    return run.map ~= "" and run.map or "the arena"
end

local function FillHeading()
    local color
    if run.victory then
        color = TITLE_VICTORY
        panel.title:SetText("Victory")
        panel.subtitle:SetText("You cleared every stage of " .. Place() .. " in " .. UI.FormatTime(run.seconds) .. ".")
    elseif run.wentHome then
        color = TITLE_HOME
        panel.title:SetText("Run Complete")
        panel.subtitle:SetText("You left " .. Place() .. " on stage " .. (run.stage or "1") .. " after " .. UI.FormatTime(run.seconds) .. ".")
    else
        color = TITLE_DEFEAT
        panel.title:SetText("Defeat")
        panel.subtitle:SetText("You fell on stage " .. (run.stage or "1") .. " in " .. Place() .. " after " .. UI.FormatTime(run.seconds) .. ".")
    end
    panel.title:SetTextColor(color[1], color[2], color[3])
    if run.victory or run.wentHome then
        screen.wound:Hide()
    else
        screen.wound:Show()
    end

    local shards = tonumber(run.shards or "") or 0
    local tickets = "|cffc080ff" .. shards .. (shards == 1 and " Darkmoon Ticket" or " Darkmoon Tickets") .. "|r"
    if run.wentHome then
        panel.earned:SetText("You earned " .. UI.FormatGold(run.gold) .. ", " .. tickets .. " and the gear in the Extraction Vault.")
    else
        panel.earned:SetText("You earned " .. UI.FormatGold(run.gold) .. " and " .. tickets .. ".")
    end
end

local function LayoutFooter(y, buildWidth)
    panel.footerRule:ClearAllPoints()
    panel.footerRule:SetPoint("TOPLEFT", panel, "TOPLEFT", PAD_X, -y)
    y = y + 8
    build:ClearAllPoints()
    build:SetPoint("TOPLEFT", panel, "TOPLEFT", PAD_X, -y)
    panel.done:ClearAllPoints()
    panel.done:SetPoint("TOPRIGHT", panel, "TOPRIGHT", -PAD_X, -y - 1)
    local left = PAD_X + buildWidth + 12
    local right = PANEL_WIDTH - PAD_X - BUTTON_WIDTH - 12
    panel.earned:SetWidth(max(80, right - left))
    panel.earned:ClearAllPoints()
    panel.earned:SetPoint("LEFT", panel, "TOPLEFT", left, -y - FOOTER_HEIGHT / 2)
    return y + FOOTER_HEIGHT
end

local function ShowSummary()
    if VanillaBonkResults then
        VanillaBonkResults:Hide()
    end

    FillHeading()

    table.sort(weapons, function(a, b) return a.damage > b.damage end)
    local total, top = 0, 0
    for _, weapon in ipairs(weapons) do
        total = total + weapon.damage
        top = max(top, weapon.damage)
    end

    local entries = {}
    for _, weapon in ipairs(weapons) do
        tinsert(entries, weapon)
        table.sort(weapon.subs, function(a, b) return a.damage > b.damage end)
        for _, sub in ipairs(weapon.subs) do
            sub.parent = weapon
            tinsert(entries, sub)
        end
    end

    local y = PAD_TOP + HEADER_HEIGHT
    for index, line in ipairs(panel.lines) do
        local entry = entries[index]
        if entry then
            y = y + FillLine(line, entry, top, total, y)
        else
            line:Hide()
        end
    end
    y = FillTypes(y + 3, total)

    local columnBottom = max(y, PAD_TOP + STATS_TOP + 7 * STAT_ROW)
    local divider = columnBottom - PAD_TOP
    panel.divider:SetHeight(divider / 2)
    panel.dividerLow:SetHeight(divider / 2)
    y = LayoutFooter(columnBottom + 12, FillBuild())
    panel:SetHeight(y + PAD_BOTTOM)
    FillStats()

    screen.closing = false
    screen.alpha = 0
    screen:SetAlpha(0)
    screen:Show()
end

local function DamageTypeFor(name, icon)
    return UI.DAMAGE_TYPE_BY_KEYWORD[name or ""] or UI.DamageTypeFromIcon(icon)
end

local handlers = {}

handlers["start"] = function(f)
    run.map = f[2] or ""
    run.difficulty = f[3] or ""
    passives = {}
    screen:Hide()
end

handlers["loadout"] = function(f)
    passives = {}
    for _, item in ipairs(UI.Split(f[2] or "", ",")) do
        local parts = UI.Split(item, ":")
        if parts[1] == "p" and parts[2] and parts[2] ~= "" then
            tinsert(passives, parts[2])
        end
    end
end

handlers["end"] = function(f)
    run = {
        map = run.map or "", difficulty = run.difficulty or "",
        wentHome = f[2] == "1", seconds = f[3], kills = f[4], gold = f[5], level = f[6], stage = f[7], shards = f[8],
        victory = f[9] == "1",
    }
    weapons = {}
end

handlers["sum"] = function(f)
    run.damageTaken = tonumber(f[2]) or 0
    run.elites = tonumber(f[3]) or 0
    run.minis = tonumber(f[4]) or 0
    run.bosses = tonumber(f[5]) or 0
end

handlers["sumw"] = function(f)
    local icon = f[2] ~= "" and f[2] or "INV_Misc_QuestionMark"
    local name = f[3] or ""
    local school = UI.DAMAGE_TYPES[f[9] or ""] and f[9] or DamageTypeFor(name, icon)
    tinsert(weapons, {
        icon = icon,
        name = name,
        level = tonumber(f[4]) or 1,
        maxLevel = tonumber(f[5]) or 0,
        evolved = f[6] == "1",
        damage = tonumber(f[7]) or 0,
        kills = tonumber(f[8]) or 0,
        school = school,
        subs = {},
    })
end

handlers["sums"] = function(f)
    local weapon = weapons[getn(weapons)]
    if not weapon then
        return
    end
    local icon = f[2] ~= "" and f[2] or "INV_Misc_QuestionMark"
    tinsert(weapon.subs, {
        icon = icon,
        name = f[3] or "",
        damage = tonumber(f[4]) or 0,
        kills = tonumber(f[5]) or 0,
        school = DamageTypeFor(f[3], icon),
    })
end

handlers["sumend"] = ShowSummary

UI.Listen(handlers)
