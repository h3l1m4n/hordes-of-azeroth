local MAX_CAPTURE_BARS = 5

local function HideWorldStateUI()
    if WorldStateAlwaysUpFrame then
        WorldStateAlwaysUpFrame:Hide()
    end
    for index = 1, MAX_CAPTURE_BARS do
        local bar = getglobal("WorldStateCaptureBar" .. index)
        if bar then
            bar:Hide()
        end
    end
end

if WorldStateAlwaysUpFrame then
    WorldStateAlwaysUpFrame:SetScript("OnShow", function()
        this:Hide()
    end)
end

WorldStateAlwaysUpFrame_Update = HideWorldStateUI

local watcher = CreateFrame("Frame")
watcher:RegisterEvent("PLAYER_ENTERING_WORLD")
watcher:RegisterEvent("UPDATE_WORLD_STATES")
watcher:RegisterEvent("ZONE_CHANGED_NEW_AREA")
watcher:SetScript("OnEvent", HideWorldStateUI)
HideWorldStateUI()
