local UI = VanillaBonkUI

local LIFT = 0
local NUDGE_X = 0
local NORMAL_KIND = 0
local COLOR = { 0.24, 0.70, 0.28 }
local BAR_TEXTURE = "Interface\\TargetingFrame\\UI-StatusBar"

UI.HEALTH_BAR_MODES = {
    { value = "frame", label = "Unit Frame" },
    { value = "plate", label = "Nameplate" },
    { value = "both", label = "Both" },
}

local shield = 0
local activeMode

local bar = UI.SamplePlate(UIParent, NORMAL_KIND)
bar:SetFrameStrata("BACKGROUND")
bar:SetMinMaxValues(0, 1)
bar:Hide()

local shine = bar:CreateTexture(nil, "OVERLAY")
shine:SetTexture(BAR_TEXTURE)
shine:SetBlendMode("ADD")
shine:SetVertexColor(UI.SHIELD_COLOR[1], UI.SHIELD_COLOR[2], UI.SHIELD_COLOR[3], UI.SHIELD_COLOR[4])
shine:SetPoint("TOPLEFT", bar, "TOPLEFT", 0, 0)
shine:SetPoint("BOTTOMLEFT", bar, "BOTTOMLEFT", 0, 0)
shine:Hide()

function UI.HealthBarMode()
    local mode = UI.Option("healthBar", nil)
    if mode then
        return mode
    end
    local selfBar = UI.Option("selfBar", nil)
    if selfBar == nil then
        return "plate"
    end
    return selfBar and "both" or "frame"
end

local function ActiveMode()
    return activeMode or UI.HealthBarMode()
end

function UI.ApplyHealthBarMode(mode)
    activeMode = mode or UI.HealthBarMode()
    if UI.PlaceDarkmoonCards then
        UI.PlaceDarkmoonCards(activeMode == "plate")
    end
    if activeMode == "plate" then
        PlayerFrame:Hide()
    else
        PlayerFrame:Show()
    end
end

local BasePlayerFrameShow = PlayerFrame:GetScript("OnShow")
PlayerFrame:SetScript("OnShow", function()
    if ActiveMode() == "plate" then
        this:Hide()
        return
    end
    if BasePlayerFrameShow then
        BasePlayerFrameShow()
    end
end)

local function Wanted()
    return ActiveMode() ~= "frame" and HoAController_GetPlayerHead and not UnitIsDeadOrGhost("player")
end

local function RefreshShield(maxHealth)
    local share = maxHealth > 0 and math.min(1, shield / maxHealth) or 0
    if share <= 0 then
        shine:Hide()
        return
    end
    shine:SetWidth(bar:GetWidth() * share)
    shine:SetTexCoord(0, share, 0, 1)
    shine:Show()
end

local function Refresh()
    local maxHealth = UnitHealthMax("player")
    local share = maxHealth > 0 and UnitHealth("player") / maxHealth or 0
    bar:SetValue(share)
    bar:SetStatusBarColor(COLOR[1], COLOR[2], COLOR[3])
    RefreshShield(maxHealth)
end

local driver = CreateFrame("Frame")
driver:SetScript("OnUpdate", function()
    if not Wanted() then
        bar:Hide()
        return
    end
    local x, y = HoAController_GetPlayerHead()
    if not x then
        bar:Hide()
        return
    end
    local worldScale = WorldFrame:GetEffectiveScale()
    local scale = bar:GetEffectiveScale()
    bar:ClearAllPoints()
    bar:SetPoint("BOTTOM", WorldFrame, "BOTTOMLEFT", x * WorldFrame:GetWidth() * worldScale / scale + NUDGE_X,
        y * WorldFrame:GetHeight() * worldScale / scale + LIFT)
    Refresh()
    bar:Show()
end)
driver:RegisterEvent("PLAYER_ENTERING_WORLD")
driver:SetScript("OnEvent", function()
    UI.ApplyHealthBarMode()
end)

UI.Listen({
    shield = function(f)
        shield = tonumber(f[2]) or 0
    end,
})

UI.SelfBar = bar
