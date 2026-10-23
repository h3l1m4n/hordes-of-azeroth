local SANDBOX_AT = 4
local SETUP_AT = 8
local SETUP_GAP = 1
local BUILD_GAP = 1.2
local BUILD_SECONDS = BenchBuildSeconds or 45
local FALLBACK_QUIT_DELAY = 15

local steps = {}

local function at(seconds, action)
    table.insert(steps, { t = seconds, f = action })
end

local function sandbox(verb)
    local text = ".bonk sandbox"
    if verb then
        text = text .. " " .. verb
    end
    SendChatMessage(text, "SAY")
end

local function mark(text)
    SendChatMessage(text, "WHISPER", nil, UnitName("player"))
end

local function scheduleBuild(build, start)
    at(start, function() sandbox("clear") end)
    at(start + 0.5, function() sandbox("forget 0") end)
    for i = 1, table.getn(build.steps) do
        local verb = build.steps[i]
        at(start + 1 + i * BUILD_GAP, function() sandbox(verb) end)
    end
    local ready = start + 1.5 + (table.getn(build.steps) + 1) * BUILD_GAP
    at(ready, function() sandbox("heal") end)
    at(ready + 1.5, function() sandbox(BenchDummies and ("dummies " .. BenchDummies) or BenchPack or "pack 150") end)
    at(ready + 2.7, function() mark("window pack " .. build.name) end)
end

local function scheduleAll()
    at(1, function() mark("bench start") end)
    at(SANDBOX_AT, function() sandbox() end)
    local t = SETUP_AT
    for i = 1, table.getn(BenchSetup) do
        local verb = BenchSetup[i]
        at(t, function() sandbox(verb) end)
        t = t + SETUP_GAP
    end
    t = t + SETUP_GAP
    for b = 1, table.getn(BenchBuilds) do
        scheduleBuild(BenchBuilds[b], t)
        t = t + BUILD_SECONDS
    end
    at(t, function()
        mark("bench done")
        sandbox("clear")
    end)
    at(t + 1, function() sandbox("forget 0") end)
    at(t + FALLBACK_QUIT_DELAY, function() ForceQuit() end)
end

scheduleAll()

local frame = CreateFrame("Frame")
local started, index = nil, 1
frame:RegisterEvent("PLAYER_ENTERING_WORLD")
frame:SetScript("OnEvent", function()
    VanillaBonkPlateGuideDone = true
    LoggingChat(1)
    if not started then
        started = GetTime()
    end
end)
frame:SetScript("OnUpdate", function()
    if not started then
        return
    end
    local elapsed = GetTime() - started
    while steps[index] and steps[index].t <= elapsed do
        local step = steps[index]
        index = index + 1
        pcall(step.f)
    end
end)
