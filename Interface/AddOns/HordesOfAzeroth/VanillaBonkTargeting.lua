local UI = VanillaBonkUI

local inRun = false

local function IsEnemyTarget()
    return UnitExists("target") and not UnitIsUnit("target", "player") and UnitCanAttack("player", "target")
end

local function DropEnemyTarget()
    if inRun and IsEnemyTarget() then
        ClearTarget()
    end
end

local BaseErrorsOnEvent = UIErrorsFrame:GetScript("OnEvent")
UIErrorsFrame:SetScript("OnEvent", function()
    if inRun and event == "UI_ERROR_MESSAGE" and arg1 == ERR_NO_ATTACK_TARGET then
        return
    end
    if BaseErrorsOnEvent then
        BaseErrorsOnEvent()
    end
end)

local events = CreateFrame("Frame")
events:RegisterEvent("PLAYER_TARGET_CHANGED")
events:SetScript("OnEvent", DropEnemyTarget)

local function EnterRun()
    inRun = true
    DropEnemyTarget()
end

UI.Listen({
    start = EnterRun,
    state = EnterRun,
    ["end"] = function()
        inRun = false
    end,
})
