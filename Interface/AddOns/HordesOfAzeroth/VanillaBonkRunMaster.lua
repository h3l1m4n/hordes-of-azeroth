local UI = VanillaBonkUI

VanillaBonkRunMaster = {}
local RunMaster = VanillaBonkRunMaster

local LEFT_X = 38
local LIST_TOP = 166
local ROW_HEIGHT = 36
local ROW_STEP = 38
local ROW_WIDTH = 312
local ROW_ICON = 28
local BADGE_SIZE = 14
local CHALLENGE_TOP = 498
local CHALLENGE_ICON = 28
local CHALLENGE_STEP = 36
local PREVIEW_X = 372
local PREVIEW_WIDTH = 476
local PREVIEW_HEIGHT = 268
local ART_WIDTH = 196
local ART_HEIGHT = 244
local PREVIEW_TEXT_X = 222
local PREVIEW_TEXT_WIDTH = PREVIEW_WIDTH - PREVIEW_TEXT_X - 14
local ROUTE_ICON = 26
local ROUTE_STEP = 46
local RANDOM_ICON = "INV_Misc_QuestionMark"
local INVADED_COLOR = { 1, 0.6, 0.24 }
local CHALLENGE_RISK = "Enemies have 30% more health and deal 20% more damage."
local DUNGEON_REWARD = "Enemies drop 50% more gold, and loot is one quality higher."
local INVASION_REWARD = "More enemies come at once. They drop 25% more gold, and loot has a 50% chance to be one quality higher."
local CONTINENT_ART = {
    ["0"] = "Interface\\Glues\\LoadingScreens\\LoadScreenEasternKingdom",
    ["1"] = "Interface\\Glues\\LoadingScreens\\LoadScreenKalimdor",
}
local DUNGEON_ART = {
    moltencore = "Interface\\Glues\\LoadingScreens\\LoadScreenMoltenCore",
    zulgurub = "Interface\\Glues\\LoadingScreens\\LoadScreenZulGurub",
    ruinsaq = "Interface\\Glues\\LoadingScreens\\LoadScreenAhnQiraj20man",
}
local MAP_TYPES = { ["0"] = "Open zone", ["1"] = "Enclosed raid", ["2"] = "Dungeon room" }
local WORLD_MAP_PATH = "Interface\\WorldMap\\"
local ZONE_MAP_WIDTH, ZONE_MAP_HEIGHT = 1002, 668
local MAP_TILE = 256
local MAP_TILE_COLUMNS, MAP_TILE_ROWS = 4, 3
local MAP_CROP = 0.42
local INVASION_TINT = {
    moltencore = { 1, 0.42, 0.28 },
    zulgurub = { 0.55, 0.85, 0.45 },
    ruinsaq = { 1, 0.82, 0.45 },
}
local DEFAULT_TINT = { 1, 0.6, 0.45 }

local state = { zone = {}, dungeon = {}, invade = {} }
local mapInfo = {}
local zoneOverlays, overlaySeen = {}, {}
local arena, content, tier
local rows, challengeSlots = {}, {}
local preview, challenge

local function Icon(name)
    return UI.ICON_PATH .. ((name and name ~= "") and name or RANDOM_ICON)
end

local function Multiplier(percent)
    local text = format("%.2f", percent / 100)
    text = gsub(text, "0$", "")
    return gsub(text, "%.$", ".0")
end

local function Info(key)
    return mapInfo[key or ""] or {}
end

local function Colored(text, r, g, b)
    return format("|cff%02x%02x%02x%s|r", floor(r * 255), floor(g * 255), floor(b * 255), text)
end

local function ChosenZone()
    local key = state.zone[tier.id]
    for _, map in ipairs(tier.maps) do
        if map.key == key then
            return map
        end
    end
    state.zone[tier.id] = nil
end

local function ChosenDungeon()
    local key = state.dungeon[tier.id]
    for _, dungeon in ipairs(tier.dungeons or {}) do
        if dungeon.key == key and dungeon.open then
            return dungeon
        end
    end
    state.dungeon[tier.id] = nil
end

local function IsInvasion()
    return ChosenDungeon() ~= nil and state.invade[tier.id] == true
end

local function IsDungeonMode()
    return ChosenDungeon() ~= nil and not state.invade[tier.id]
end

local function Badge(parent, anchor)
    local badge = parent:CreateTexture(nil, "OVERLAY")
    badge:SetWidth(BADGE_SIZE)
    badge:SetHeight(BADGE_SIZE)
    badge:SetPoint("BOTTOMRIGHT", anchor, "BOTTOMRIGHT", 4, -4)
    badge:SetTexCoord(0.08, 0.92, 0.08, 0.92)
    badge:Hide()
    return badge
end

local function SetBadge(badge, dungeon)
    if dungeon then
        badge:SetTexture(Icon(dungeon.icon))
        badge:Show()
    else
        badge:Hide()
    end
end

local function RowTooltip(row)
    local map = row.map
    GameTooltip:SetOwner(row, "ANCHOR_RIGHT")
    if not map then
        GameTooltip:AddLine("Random zone", 1, 0.82, 0)
        GameTooltip:AddLine("Stage 1 takes you to any zone of " .. tier.name .. ".", 1, 1, 1, 1)
    else
        GameTooltip:AddLine(map.name, 1, 0.82, 0)
        if MAP_TYPES[map.type] then
            GameTooltip:AddLine(MAP_TYPES[map.type], 0.7, 0.7, 0.7)
        end
        local info = Info(map.key)
        if info.description and info.description ~= "" then
            GameTooltip:AddLine(info.description, 1, 1, 1, 1)
        end
        if map.reward and map.reward ~= 100 then
            GameTooltip:AddLine("Pays " .. Multiplier(map.reward) .. "x gold, Darkmoon Tickets and Mastery experience.", 0.25, 1, 0.25, 1)
        end
    end
    if IsDungeonMode() then
        GameTooltip:AddLine("Dungeon mode plays every stage inside " .. ChosenDungeon().name .. ".", 1, 0.3, 0.3, 1)
    elseif tier.stages and tier.stages > 1 then
        GameTooltip:AddLine("Your run starts here. Later stages are random.", 1, 0.82, 0, 1)
    end
    GameTooltip:Show()
end

local function MakeRow(index)
    local row = CreateFrame("Button", nil, content)
    row:SetWidth(ROW_WIDTH)
    row:SetHeight(ROW_HEIGHT)
    row:SetPoint("TOPLEFT", arena, "TOPLEFT", LEFT_X - 4, -(LIST_TOP + 22 + (index - 1) * ROW_STEP))
    row:SetBackdrop({ bgFile = UI.FLAT, edgeFile = UI.FLAT, edgeSize = 1, insets = { left = 1, right = 1, top = 1, bottom = 1 } })
    row:SetHighlightTexture("Interface\\QuestFrame\\UI-QuestTitleHighlight", "ADD")
    row.icon = row:CreateTexture(nil, "ARTWORK")
    row.icon:SetWidth(ROW_ICON)
    row.icon:SetHeight(ROW_ICON)
    row.icon:SetPoint("LEFT", row, "LEFT", 5, 0)
    row.icon:SetTexCoord(0.07, 0.93, 0.07, 0.93)
    row.badge = Badge(row, row.icon)
    row.name = UI.Text(row, 13)
    row.name:SetPoint("TOPLEFT", row.icon, "TOPRIGHT", 9, 1)
    row.name:SetJustifyH("LEFT")
    row.summary = UI.Text(row, 10)
    row.summary:SetPoint("TOPLEFT", row.name, "BOTTOMLEFT", 0, -2)
    row.summary:SetWidth(ROW_WIDTH - ROW_ICON - 22)
    row.summary:SetJustifyH("LEFT")
    row.summary:SetTextColor(0.8, 0.8, 0.8)
    row:SetScript("OnClick", function()
        if IsDungeonMode() or not tier.unlocked then
            return
        end
        PlaySound("igMainMenuOptionCheckBoxOn")
        state.zone[tier.id] = this.map and this.map.key or nil
        RunMaster.Show(tier)
    end)
    row:SetScript("OnEnter", function() RowTooltip(this) end)
    row:SetScript("OnLeave", function() GameTooltip:Hide() end)
    rows[index] = row
    return row
end

local function FillRow(row, map, chosen, invader, dimmed)
    row.map = map
    row.icon:SetTexture(Icon(map and map.icon))
    row.name:SetText(map and map.name or "Random zone")
    row.summary:SetText(map and (Info(map.key).summary or "") or ("Any " .. tier.name .. " zone, picked when the run starts"))
    SetBadge(row.badge, map and invader or nil)
    SetDesaturation(row.icon, dimmed)
    local lit = chosen and not dimmed
    UI.SetStripHighlight(row, lit)
    if dimmed then
        row.name:SetTextColor(0.5, 0.5, 0.5)
    elseif lit then
        row.name:SetTextColor(1, 1, 1)
    else
        row.name:SetTextColor(1, 0.82, 0)
    end
    row:Show()
end

local function ShowRows()
    local zone = ChosenZone()
    local invader = IsInvasion() and ChosenDungeon() or nil
    local dimmed = IsDungeonMode() or not tier.unlocked
    for _, row in ipairs(rows) do
        row:Hide()
    end
    FillRow(rows[1] or MakeRow(1), nil, zone == nil, nil, dimmed)
    for index, map in ipairs(tier.maps) do
        FillRow(rows[index + 1] or MakeRow(index + 1), map, zone == map, invader, dimmed)
    end
end

local function ChallengeTooltip(slot)
    local dungeon = slot.dungeon
    GameTooltip:SetOwner(slot, "ANCHOR_RIGHT")
    GameTooltip:AddLine(dungeon.name, dungeon.open and 1 or 0.6, dungeon.open and 0.82 or 0.6, dungeon.open and 0 or 0.6)
    GameTooltip:AddLine("Challenge mode", 0.7, 0.7, 0.7)
    GameTooltip:AddLine(CHALLENGE_RISK, 1, 0.5, 0.5, 1)
    GameTooltip:AddLine("Dungeon: every stage takes place inside " .. dungeon.name .. ".", 1, 1, 1, 1)
    GameTooltip:AddLine(DUNGEON_REWARD, 0.25, 1, 0.25, 1)
    GameTooltip:AddLine("Invasion: " .. dungeon.name .. "'s enemies and lords fill the zone you pick.", 1, 1, 1, 1)
    GameTooltip:AddLine(INVASION_REWARD, 0.25, 1, 0.25, 1)
    if not dungeon.open then
        GameTooltip:AddLine("Clear " .. tier.name .. " first.", 1, 0.3, 0.3)
    elseif state.dungeon[tier.id] == dungeon.key then
        GameTooltip:AddLine("Click again to turn it off.", 1, 0.82, 0, 1)
    end
    GameTooltip:Show()
end

local function MakeChallengeSlot(index)
    local slot = UI.Slot(content, CHALLENGE_ICON)
    slot:SetPoint("TOPLEFT", arena, "TOPLEFT", LEFT_X + 2 + (index - 1) * CHALLENGE_STEP, -(CHALLENGE_TOP + 22))
    slot.glow = slot:CreateTexture(nil, "OVERLAY")
    slot.glow:SetTexture("Interface\\Buttons\\UI-ActionButton-Border")
    slot.glow:SetBlendMode("ADD")
    slot.glow:SetVertexColor(1, 0.82, 0)
    slot.glow:SetWidth(CHALLENGE_ICON * 1.8)
    slot.glow:SetHeight(CHALLENGE_ICON * 1.8)
    slot.glow:SetPoint("CENTER", slot, "CENTER", 0, 0)
    slot:SetScript("OnClick", function()
        local dungeon = this.dungeon
        if not dungeon or not dungeon.open or not tier.unlocked then
            return
        end
        if state.dungeon[tier.id] == dungeon.key then
            PlaySound("igMainMenuOptionCheckBoxOff")
            state.dungeon[tier.id] = nil
        else
            PlaySound("igMainMenuOptionCheckBoxOn")
            state.dungeon[tier.id] = dungeon.key
        end
        RunMaster.Show(tier)
    end)
    slot:SetScript("OnEnter", function() ChallengeTooltip(this) end)
    slot:SetScript("OnLeave", function() GameTooltip:Hide() end)
    challengeSlots[index] = slot
    return slot
end

local function SetMode(invade)
    if not ChosenDungeon() or state.invade[tier.id] == invade then
        return
    end
    PlaySound("igMainMenuOptionCheckBoxOn")
    state.invade[tier.id] = invade
    RunMaster.Show(tier)
end

local function StyleToggle(button, on, enabled)
    if on and enabled then
        button:LockHighlight()
        button:SetTextColor(1, 1, 1)
    else
        button:UnlockHighlight()
        if enabled then
            button:SetTextColor(1, 0.82, 0)
        else
            button:SetTextColor(0.5, 0.5, 0.5)
        end
    end
end

local function ShowChallenges()
    local dungeons = tier.dungeons or {}
    for _, slot in ipairs(challengeSlots) do
        slot:Hide()
    end
    if table.getn(dungeons) == 0 then
        challenge.label:Hide()
        challenge.dungeon:Hide()
        challenge.invasion:Hide()
        challenge.hint:SetText("")
        return
    end
    challenge.label:Show()
    local chosen = ChosenDungeon()
    for index, dungeon in ipairs(dungeons) do
        local slot = challengeSlots[index] or MakeChallengeSlot(index)
        slot.dungeon = dungeon
        slot.icon:SetTexture(Icon(dungeon.icon))
        SetDesaturation(slot.icon, not dungeon.open or not tier.unlocked)
        if chosen == dungeon then
            slot.glow:Show()
        else
            slot.glow:Hide()
        end
        slot:Show()
    end
    local left = LEFT_X + 10 + table.getn(dungeons) * CHALLENGE_STEP
    challenge.dungeon:ClearAllPoints()
    challenge.dungeon:SetPoint("TOPLEFT", arena, "TOPLEFT", left, -(CHALLENGE_TOP + 20))
    challenge.dungeon:Show()
    challenge.invasion:Show()
    StyleToggle(challenge.dungeon, chosen and not IsInvasion(), chosen ~= nil)
    StyleToggle(challenge.invasion, IsInvasion(), chosen ~= nil)
    if chosen then
        challenge.hint:SetText("|cff9d9d9dClick the lit icon again to turn it off.|r")
    elseif dungeons[1] and not dungeons[1].open then
        challenge.hint:SetText("|cff9d9d9dClear " .. tier.name .. " to unlock challenges.|r")
    else
        challenge.hint:SetText("|cff9d9d9dPick a dungeon to make the run harder and richer.|r")
    end
end

local function MapTexture(index, layer)
    local canvas = preview.map.canvas
    local texture = canvas.textures[index]
    if not texture then
        texture = canvas:CreateTexture(nil, layer)
        canvas.textures[index] = texture
    end
    texture:SetDrawLayer(layer)
    texture:ClearAllPoints()
    return texture
end

local function PlaceMapTexture(index, layer, path, x, y, width, height, fileWidth, fileHeight, scale)
    local texture = MapTexture(index, layer)
    texture:SetTexture(path)
    texture:SetTexCoord(0, width / fileWidth, 0, height / fileHeight)
    texture:SetWidth(width * scale)
    texture:SetHeight(height * scale)
    texture:SetPoint("TOPLEFT", preview.map.canvas, "TOPLEFT", x * scale, -y * scale)
    texture:Show()
    return index + 1
end

local function FilePixels(pixels)
    local size = 16
    while size < pixels do
        size = size * 2
    end
    return size
end

local function PlaceOverlay(index, file, overlay, scale)
    local wide, tall = ceil(overlay.width / MAP_TILE), ceil(overlay.height / MAP_TILE)
    for row = 1, tall do
        local height, fileHeight = MAP_TILE, MAP_TILE
        if row == tall then
            height = mod(overlay.height, MAP_TILE)
            if height == 0 then
                height = MAP_TILE
            end
            fileHeight = FilePixels(height)
        end
        for column = 1, wide do
            local width, fileWidth = MAP_TILE, MAP_TILE
            if column == wide then
                width = mod(overlay.width, MAP_TILE)
                if width == 0 then
                    width = MAP_TILE
                end
                fileWidth = FilePixels(width)
            end
            local path = WORLD_MAP_PATH .. file .. "\\" .. overlay.texture .. ((row - 1) * wide + column)
            index = PlaceMapTexture(index, "ARTWORK", path, overlay.x + MAP_TILE * (column - 1), overlay.y + MAP_TILE * (row - 1),
                width, height, fileWidth, fileHeight, scale)
        end
    end
    return index
end

local function DrawZoneMap(file)
    local map = preview.map
    if map.file == file then
        return
    end
    map.file = file
    local scale = ART_WIDTH / (ZONE_MAP_WIDTH * MAP_CROP)
    map.scale = scale
    map.canvas:SetWidth(ZONE_MAP_WIDTH * scale)
    map.canvas:SetHeight(ZONE_MAP_HEIGHT * scale)
    local index = 1
    for tile = 1, MAP_TILE_COLUMNS * MAP_TILE_ROWS do
        index = PlaceMapTexture(index, "BACKGROUND", WORLD_MAP_PATH .. file .. "\\" .. file .. tile,
            mod(tile - 1, MAP_TILE_COLUMNS) * MAP_TILE, floor((tile - 1) / MAP_TILE_COLUMNS) * MAP_TILE, MAP_TILE, MAP_TILE, MAP_TILE, MAP_TILE, scale)
    end
    for _, overlay in ipairs(zoneOverlays[file] or {}) do
        index = PlaceOverlay(index, file, overlay, scale)
    end
    for unused = index, table.getn(map.canvas.textures) do
        map.canvas.textures[unused]:Hide()
    end
end

local function Clamp(value, low, high)
    return max(low, min(high, value))
end

local function ShowZoneMap(info, tint, locked)
    local map = preview.map
    DrawZoneMap(info.file)
    local scale = map.scale
    local x = Clamp((info.u or 0.5) * ZONE_MAP_WIDTH * scale - ART_WIDTH / 2, 0, ZONE_MAP_WIDTH * scale - ART_WIDTH)
    local y = Clamp((info.v or 0.5) * ZONE_MAP_HEIGHT * scale - ART_HEIGHT / 2, 0, ZONE_MAP_HEIGHT * scale - ART_HEIGHT)
    map:SetHorizontalScroll(-x)
    map:SetVerticalScroll(y)
    local r, g, b = 1, 1, 1
    if tint then
        r, g, b = tint[1], tint[2], tint[3]
    end
    for _, texture in ipairs(map.canvas.textures) do
        texture:SetVertexColor(r, g, b)
        SetDesaturation(texture, locked)
    end
    preview.art:Hide()
    map:Show()
end

local function SetArt(path, invaded, locked)
    preview.map:Hide()
    preview.art:Show()
    preview.art:SetTexture(path)
    UI.LoadingScreenCoords(preview.art, ART_WIDTH, ART_HEIGHT, 0.5)
    if invaded then
        preview.art:SetVertexColor(1, 0.7, 0.5)
    else
        preview.art:SetVertexColor(1, 1, 1)
    end
    SetDesaturation(preview.art, locked)
end

local function RouteIcon(index)
    local slot = preview.route[index]
    if slot then
        return slot
    end
    slot = UI.Slot(preview, ROUTE_ICON, "Frame")
    slot.badge = Badge(slot, slot)
    slot.arrow = UI.Text(preview, 14)
    slot.arrow:SetTextColor(1, 0.82, 0)
    slot.arrow:SetText(">")
    preview.route[index] = slot
    return slot
end

local function StageWord(count)
    if count == 1 then
        return "stage 2"
    elseif count == 2 then
        return "stages 2 and 3"
    end
    return "stages 2 to " .. (count + 1)
end

local function ShowRoute(firstIcon, laterIcon, badge, caption)
    local stages = tier.stages or 1
    for _, slot in ipairs(preview.route) do
        slot:Hide()
        slot.arrow:Hide()
    end
    for index = 1, stages do
        local slot = RouteIcon(index)
        slot:ClearAllPoints()
        slot:SetPoint("TOPLEFT", preview.routeLabel, "BOTTOMLEFT", 2 + (index - 1) * ROUTE_STEP, -8)
        slot.icon:SetTexture(Icon(index == 1 and firstIcon or laterIcon))
        SetBadge(slot.badge, badge)
        slot:Show()
        if index > 1 then
            slot.arrow:ClearAllPoints()
            slot.arrow:SetPoint("RIGHT", slot, "LEFT", -7, 0)
            slot.arrow:Show()
        end
    end
    preview.endless:ClearAllPoints()
    preview.endless:SetPoint("LEFT", RouteIcon(stages), "RIGHT", 10, 0)
    preview.endless:SetText(tier.endless and "|cff9d9d9dthen Endless|r" or "")
    preview.caption:SetText(caption)
end

local function ShowPreview()
    local zone = ChosenZone()
    local dungeon = ChosenDungeon()
    local stages = tier.stages or 1
    local later = stages > 1 and ("|cff9d9d9d, " .. StageWord(stages - 1) .. " random|r") or ""
    local locked = not tier.unlocked
    if IsDungeonMode() then
        SetArt(DUNGEON_ART[dungeon.key] or tier.art, false, locked)
        preview.name:SetText(dungeon.name)
        preview.subtitle:SetText(Colored("Dungeon challenge", unpack(INVADED_COLOR)))
        preview.description:SetText(Info(dungeon.key).description or "")
        ShowRoute(dungeon.icon, dungeon.icon, nil, "|cffffffffEvery stage inside " .. dungeon.name .. ".|r")
    else
        local invader = IsInvasion() and dungeon or nil
        local info = Info(zone and zone.key)
        if zone and info.file and info.file ~= "" then
            ShowZoneMap(info, invader and (INVASION_TINT[invader.key] or DEFAULT_TINT), locked)
        else
            local art = zone and CONTINENT_ART[info.continent or ""] or tier.art
            SetArt(art ~= "" and art or CONTINENT_ART["0"], invader ~= nil, locked)
        end
        preview.name:SetText(zone and zone.name or "Random zone")
        local place = zone and zone.name or "the zone"
        if invader then
            preview.subtitle:SetText(Colored("Invaded by " .. invader.name, unpack(INVADED_COLOR)))
            local summary = Info(invader.key).summary or "Its enemies"
            preview.description:SetText(summary .. " roam " .. place .. ". " .. invader.name .. "'s lords lead every stage.")
        else
            preview.subtitle:SetText("")
            preview.description:SetText(zone and (info.description or "") or ("Stage 1 takes you to any zone of " .. tier.name .. "."))
        end
        local first = zone and "|cffffffffStage 1 here|r" or "|cffffffffEvery stage random|r"
        ShowRoute(zone and zone.icon or RANDOM_ICON, RANDOM_ICON, invader, zone and (first .. later) or first)
    end
    if dungeon then
        preview.odds:SetText(Colored(CHALLENGE_RISK, 1, 0.5, 0.5) .. "\n" .. Colored(IsInvasion() and INVASION_REWARD or DUNGEON_REWARD, 0.25, 1, 0.25))
    elseif zone and zone.reward and zone.reward ~= 100 then
        preview.odds:SetText(Colored("Pays " .. Multiplier(zone.reward) .. "x gold, Darkmoon Tickets and Mastery experience.", 0.25, 1, 0.25))
    else
        preview.odds:SetText("")
    end
end

local function Build()
    arena = VanillaBonkArena
    content = arena.content

    rows.label = UI.Text(content, 13)
    rows.label:SetPoint("TOPLEFT", arena, "TOPLEFT", LEFT_X, -LIST_TOP)
    rows.label:SetTextColor(1, 0.82, 0)
    rows.label:SetText("Starting zone")

    challenge = {}
    challenge.label = UI.Text(content, 13)
    challenge.label:SetPoint("TOPLEFT", arena, "TOPLEFT", LEFT_X, -CHALLENGE_TOP)
    challenge.label:SetTextColor(1, 0.82, 0)
    challenge.label:SetText("Challenge mode")
    challenge.dungeon = UI.Button(content, 78, "Dungeon", function() SetMode(false) end)
    challenge.invasion = UI.Button(content, 78, "Invasion", function() SetMode(true) end)
    challenge.invasion:SetPoint("LEFT", challenge.dungeon, "RIGHT", 4, 0)
    challenge.hint = UI.Text(content, 10)
    challenge.hint:SetPoint("TOPLEFT", challenge.dungeon, "BOTTOMLEFT", 0, -4)
    challenge.hint:SetWidth(200)
    challenge.hint:SetJustifyH("LEFT")

    preview = UI.Strip(content)
    preview:SetPoint("TOPLEFT", arena, "TOPLEFT", PREVIEW_X, -LIST_TOP)
    preview:SetWidth(PREVIEW_WIDTH)
    preview:SetHeight(PREVIEW_HEIGHT)
    preview.art = preview:CreateTexture(nil, "ARTWORK")
    preview.art:SetWidth(ART_WIDTH)
    preview.art:SetHeight(ART_HEIGHT)
    preview.art:SetPoint("TOPLEFT", preview, "TOPLEFT", 12, -12)
    preview.map = CreateFrame("ScrollFrame", nil, preview)
    preview.map:SetWidth(ART_WIDTH)
    preview.map:SetHeight(ART_HEIGHT)
    preview.map:SetPoint("TOPLEFT", preview, "TOPLEFT", 12, -12)
    preview.map.canvas = CreateFrame("Frame", nil, preview.map)
    preview.map.canvas.textures = {}
    preview.map.canvas:SetWidth(ZONE_MAP_WIDTH)
    preview.map.canvas:SetHeight(ZONE_MAP_HEIGHT)
    preview.map:SetScrollChild(preview.map.canvas)
    preview.map:Hide()
    preview.name = preview:CreateFontString(nil, "OVERLAY")
    preview.name:SetFont(UI.TITLE_FONT, 22, "OUTLINE")
    preview.name:SetTextColor(1, 0.82, 0)
    preview.name:SetPoint("TOPLEFT", preview, "TOPLEFT", PREVIEW_TEXT_X, -14)
    preview.name:SetWidth(PREVIEW_TEXT_WIDTH)
    preview.name:SetJustifyH("LEFT")
    preview.subtitle = UI.Text(preview, 12)
    preview.subtitle:SetPoint("TOPLEFT", preview.name, "BOTTOMLEFT", 0, -3)
    preview.description = UI.Text(preview, 11)
    preview.description:SetPoint("TOPLEFT", preview, "TOPLEFT", PREVIEW_TEXT_X, -62)
    preview.description:SetWidth(PREVIEW_TEXT_WIDTH)
    preview.description:SetJustifyH("LEFT")
    preview.routeLabel = UI.Text(preview, 12)
    preview.routeLabel:SetPoint("TOPLEFT", preview, "TOPLEFT", PREVIEW_TEXT_X, -120)
    preview.routeLabel:SetTextColor(1, 0.82, 0)
    preview.routeLabel:SetText("Your route")
    preview.route = {}
    preview.endless = UI.Text(preview, 10)
    preview.caption = UI.Text(preview, 11)
    preview.caption:SetPoint("TOPLEFT", preview.routeLabel, "BOTTOMLEFT", 0, -40)
    preview.caption:SetWidth(PREVIEW_TEXT_WIDTH)
    preview.caption:SetJustifyH("LEFT")
    preview.odds = UI.Text(preview, 10)
    preview.odds:SetPoint("TOPLEFT", preview.caption, "BOTTOMLEFT", 0, -5)
    preview.odds:SetWidth(PREVIEW_TEXT_WIDTH)
    preview.odds:SetJustifyH("LEFT")
end

function RunMaster.Show(shownTier)
    if not preview then
        Build()
    end
    tier = shownTier
    ShowRows()
    ShowChallenges()
    ShowPreview()
end

function RunMaster.StartArgs(startTier)
    tier = startTier
    local args = tier.key
    local dungeon = ChosenDungeon()
    local zone = ChosenZone()
    if zone and not IsDungeonMode() then
        args = args .. " " .. zone.key
    end
    if dungeon then
        args = args .. " " .. dungeon.key .. (IsInvasion() and " invade" or "")
    end
    return args
end

UI.Listen({
    mapinfo = function(f)
        if f[2] and f[2] ~= "" then
            mapInfo[f[2]] = { continent = f[3] or "", summary = f[4] or "", description = f[5] or "",
                file = f[6] or "", u = tonumber(f[7] or ""), v = tonumber(f[8] or "") }
        end
    end,
    zoneart = function(f)
        local file, texture = f[2], f[3]
        if not file or not texture or overlaySeen[file .. "\\" .. texture] then
            return
        end
        overlaySeen[file .. "\\" .. texture] = true
        zoneOverlays[file] = zoneOverlays[file] or {}
        tinsert(zoneOverlays[file], { texture = texture, width = tonumber(f[4]) or 0, height = tonumber(f[5]) or 0,
            x = tonumber(f[6]) or 0, y = tonumber(f[7]) or 0 })
        if preview then
            preview.map.file = nil
        end
    end,
})
