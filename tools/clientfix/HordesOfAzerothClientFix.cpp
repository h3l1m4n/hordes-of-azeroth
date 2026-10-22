#include "ClientFix.h"

#include <intrin.h>

#include <cmath>
#include <cstdarg>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <cwchar>
#include <unordered_map>

void Log(char const* format, ...)
{
    FILE* file = nullptr;
    if (fopen_s(&file, "Logs\\HordesOfAzerothClientFix.log", "a") != 0 || !file)
        return;
    fprintf(file, "%10lu ", GetTickCount());
    va_list args;
    va_start(args, format);
    vfprintf(file, format, args);
    va_end(args);
    fputc('\n', file);
    fclose(file);
}

namespace
{
    uintptr_t const FUN_MOVER_UPDATE       = 0x00616620;
    uintptr_t const FUN_MOVER_STEP         = 0x00616DE0;
    uintptr_t const FUN_JUMP               = 0x007C6230;
    uintptr_t const FUN_APPLY_KNOCKBACK    = 0x006179C0;
    uintptr_t const FUN_INPUT_MOVE         = 0x00514640;
    uintptr_t const FUN_RESOLVE_UNIT_TOKEN = 0x00515940;
    uintptr_t const FUN_APPLY_PENDING_STOP = 0x0061AB20;
    uintptr_t const LANDING_STOP_RETURN    = 0x00635A82;
    uintptr_t const FUN_SET_RUN_SPEED      = 0x007C7030;
    uintptr_t const FUN_RESTART_SEGMENT    = 0x007C5CD0;
    uintptr_t const FUN_START_MOVE         = 0x0060DC60;
    uintptr_t const FUN_MOVE_TIME_FLOOR    = 0x006169F0;
    uintptr_t const FUN_PLACE_SCREEN_RECT  = 0x00509520;
    uintptr_t const FUN_SCENE_ADVANCE      = 0x007074B0;
    uintptr_t const WORLD_SCENE_ADVANCE_RETURN = 0x0048367E;
    int const COMBAT_TEXT_RECT_BUCKET      = 1;
    uintptr_t const FUN_TICK_MS            = 0x0042B790;
    uintptr_t const VAR_INPUT_CONTROLLER   = 0x00BE1148;
    uintptr_t const VAR_INPUT_TIME         = 0x00CF0BC8;
    uintptr_t const VAR_ACTIVE_MOVER       = 0x00C4DA98;

    uint32_t const OFF_UNIT_MOVEMENT  = 0x118;
    uint32_t const OFF_MOVEMENT_OWNER = 0x15C;
    uint32_t const OFF_OBJECT_FIELDS  = 0x08;
    uint32_t const OFF_MOVEMENT_FLAGS = 0x40;
    uint32_t const OFF_FALL_TIME      = 0x78;
    uint32_t const OFF_MOVE_SPEED     = 0x84;
    uint32_t const OFF_WALK_SPEED     = 0x88;
    uint32_t const OFF_RUN_SPEED      = 0x8C;
    uint32_t const OFF_INPUT_KEYS     = 0x04;

    uint32_t const MOVEFLAG_MOVES         = 0x1 | 0x2 | 0x4 | 0x8;
    uint32_t const MOVEFLAG_ROOT          = 0x1000;
    uint32_t const MOVEFLAG_FALLING       = 0x2000 | 0x4000;
    uint32_t const MOVEFLAG_STOP_ON_LAND  = 0x8000;

    struct Axis
    {
        uint32_t note;
        uint32_t moves;
    };
    Axis const AXES[] = {
        { 0x10000, 0x1 | 0x2 },
        { 0x20000, 0x4 | 0x8 },
    };

    DWORD const KNOCKBACK_GIVE_UP_MS = 4000;
    DWORD const LANDED_WATCH_MS = 1000;

    float const DASH_SPEED_MIN = 20.0f;
    uint32_t const DASH_AIR_MS = 200;
    DWORD const DASH_STANDING_GIVE_UP_MS = 600;

    uint8_t const MOVER_UPDATE_START[]       = { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x10 };
    uint8_t const APPLY_KNOCKBACK_START[]    = { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x0C };
    uint8_t const MOVER_STEP_START[]         = { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x3C };
    uint8_t const JUMP_START[]               = { 0x55, 0x8B, 0xEC, 0x8B, 0x45, 0x08 };
    uint8_t const APPLY_PENDING_STOP_START[] = { 0x56, 0x8B, 0xF1, 0x8B, 0x46, 0x40 };
    uint8_t const SET_RUN_SPEED_START[]      = { 0x55, 0x8B, 0xEC, 0x56, 0x8B, 0xF1 };
    uint8_t const PLACE_SCREEN_RECT_START[]  = { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x30 };
    uint8_t const SCENE_ADVANCE_START[]      = { 0x55, 0x8B, 0xEC, 0x53, 0x8B, 0x5D, 0x08 };
    size_t const STOLEN_BYTES = 6;

    using MoverUpdate_t = void(__fastcall*)(void* self, void* edx, uint32_t now, uint32_t last);
    using MoverStep_t = void(__fastcall*)(void* self, void* edx, uint32_t time, uint32_t ms);
    using Jump_t = int(__fastcall*)(void* self, void* edx, int keyboard);
    using ApplyKnockback_t = void(__fastcall*)(void* self, void* edx, uint32_t a, uint32_t b, uint32_t c);
    using ApplyPendingStop_t = uint32_t(__fastcall*)(void* self, void* edx);
    using SetRunSpeed_t = int(__fastcall*)(void* self, void* edx, float speed);
    using RestartSegment_t = void(__fastcall*)(void* self, void* edx, int keepHeading);
    using InputMove_t = void(__fastcall*)(void* self, void* edx, uint32_t time, int apply);
    using StartMove_t = void(__fastcall*)(void* unit, void* edx, uint32_t time, int forward);
    using MoveTimeFloor_t = int(__fastcall*)(uint32_t* time, void* edx);
    using TickMs_t = uint32_t(__cdecl*)();
    using ResolveUnitToken_t = void*(__fastcall*)(char const* token);
    using PlaceScreenRect_t = void(__fastcall*)(int bucket, float* rect);

    PlaceScreenRect_t s_placeScreenRect = nullptr;

    using SceneAdvance_t = void(__fastcall*)(void* scene, void* edx, uint32_t ms);

    SceneAdvance_t s_sceneAdvance = nullptr;

    void __fastcall OnPlaceScreenRect(int bucket, float* rect)
    {
        if (bucket == COMBAT_TEXT_RECT_BUCKET)
            return;
        s_placeScreenRect(bucket, rect);
    }

    MoverUpdate_t s_moverUpdate = nullptr;
    MoverStep_t s_moverStep = nullptr;
    Jump_t s_jump = nullptr;
    ApplyKnockback_t s_applyKnockback = nullptr;
    ApplyPendingStop_t s_applyPendingStop = nullptr;
    SetRunSpeed_t s_setRunSpeed = nullptr;

    bool s_heldStill = false;

    enum class Knock { None, InTheAir, Landed };
    void* s_knocked = nullptr;
    DWORD s_knockedAt = 0;
    DWORD s_landedAt = 0;
    DWORD s_knockSeenAt = 0;
    Knock s_knock = Knock::None;
    uint32_t s_lastFlags = 0;

    void* s_dashStanding = nullptr;
    DWORD s_dashStandingAt = 0;
    DWORD s_dashStandingSeenAt = 0;
    void* s_airDash = nullptr;
    uint32_t s_airDashLeftMs = 0;

    template <typename T>
    T Read(void const* base, uint32_t offset)
    {
        T value;
        std::memcpy(&value, static_cast<uint8_t const*>(base) + offset, sizeof(T));
        return value;
    }

    template <typename T>
    void Write(void* base, uint32_t offset, T value)
    {
        std::memcpy(static_cast<uint8_t*>(base) + offset, &value, sizeof(T));
    }

    float AsFloat(uint32_t bits)
    {
        float value;
        std::memcpy(&value, &bits, sizeof(value));
        return value;
    }

    uint32_t MoveTimeNow()
    {
        uint32_t time = reinterpret_cast<TickMs_t>(FUN_TICK_MS)();
        uint32_t floor = 0;
        if (reinterpret_cast<MoveTimeFloor_t>(FUN_MOVE_TIME_FLOOR)(&floor, nullptr) && int32_t(time - floor) < 0)
            time = floor;
        return time;
    }

    bool IsHero(void* movement)
    {
        void* hero = reinterpret_cast<ResolveUnitToken_t>(FUN_RESOLVE_UNIT_TOKEN)("player");
        return hero && Read<void*>(hero, OFF_UNIT_MOVEMENT) == movement;
    }

    bool IsActiveMover(void* movement)
    {
        auto* owner = Read<uint8_t*>(movement, OFF_MOVEMENT_OWNER);
        auto* fields = owner ? Read<uint32_t*>(owner, OFF_OBJECT_FIELDS) : nullptr;
        auto const* active = reinterpret_cast<uint32_t const*>(VAR_ACTIVE_MOVER);
        return fields && fields[0] == active[0] && fields[1] == active[1];
    }

    bool IsPaused(void* movement)
    {
        return Read<float>(movement, OFF_WALK_SPEED) == 0.0f && Read<float>(movement, OFF_RUN_SPEED) == 0.0f;
    }

    bool IsHeroPaused()
    {
        void* hero = reinterpret_cast<ResolveUnitToken_t>(FUN_RESOLVE_UNIT_TOKEN)("player");
        void* movement = hero ? Read<void*>(hero, OFF_UNIT_MOVEMENT) : nullptr;
        return movement && IsPaused(movement);
    }

    void __fastcall OnSceneAdvance(void* scene, void* edx, uint32_t ms)
    {
        if (uintptr_t(_ReturnAddress()) == WORLD_SCENE_ADVANCE_RETURN && IsHeroPaused())
            ms = 0;
        s_sceneAdvance(scene, edx, ms);
    }

    bool ResumeHeldMovement(void* movement, uint32_t flags, uint32_t time)
    {
        auto* controller = *reinterpret_cast<uint8_t**>(VAR_INPUT_CONTROLLER);
        if (!controller)
            return false;

        auto* keys = reinterpret_cast<uint32_t*>(controller + OFF_INPUT_KEYS);
        uint32_t const before = *keys;
        for (Axis const& axis : AXES)
            if ((*keys & axis.note) && !(flags & axis.moves))
                *keys &= ~axis.note;
        if (*keys == before)
            return false;

        reinterpret_cast<InputMove_t>(FUN_INPUT_MOVE)(controller, nullptr, time, 1);
        Log("moved on: keys %#x -> %#x, flags %#x -> %#x", before, *keys, flags, Read<uint32_t>(movement, OFF_MOVEMENT_FLAGS));
        return true;
    }

    void StartStandingDash(void* movement)
    {
        auto* controller = *reinterpret_cast<uint8_t**>(VAR_INPUT_CONTROLLER);
        void* unit = Read<void*>(movement, OFF_MOVEMENT_OWNER);
        if (!controller || !unit)
            return;
        reinterpret_cast<StartMove_t>(FUN_START_MOVE)(unit, nullptr, MoveTimeNow(), 1);
        *reinterpret_cast<uint32_t*>(controller + OFF_INPUT_KEYS) |= AXES[0].note;
        s_dashStanding = movement;
        s_dashStandingAt = s_dashStandingSeenAt = GetTickCount();
        Log("dash from standing: flags %#x, keys %#x", Read<uint32_t>(movement, OFF_MOVEMENT_FLAGS), Read<uint32_t>(controller, OFF_INPUT_KEYS));
    }

    void EndStandingDash(char const* why)
    {
        void* movement = s_dashStanding;
        s_dashStanding = nullptr;
        auto* controller = *reinterpret_cast<uint8_t**>(VAR_INPUT_CONTROLLER);
        if (!controller)
            return;
        reinterpret_cast<InputMove_t>(FUN_INPUT_MOVE)(controller, nullptr, MoveTimeNow(), 1);
        Log("dash from standing over (%s): flags %#x, keys %#x", why, Read<uint32_t>(movement, OFF_MOVEMENT_FLAGS),
            Read<uint32_t>(controller, OFF_INPUT_KEYS));
    }

    void WatchStandingDash(void* movement)
    {
        DWORD const time = GetTickCount();
        DWORD const frame = time - s_dashStandingSeenAt;
        s_dashStandingSeenAt = time;
        if (IsPaused(movement))
            s_dashStandingAt += frame;
        else if (time - s_dashStandingAt > DASH_STANDING_GIVE_UP_MS)
            EndStandingDash("its end never came");
    }

    void __fastcall OnMoverUpdate(void* self, void* edx, uint32_t now, uint32_t last)
    {
        s_moverUpdate(self, edx, now, last);
        if (s_dashStanding && self == s_dashStanding)
            WatchStandingDash(self);
        if (s_knock == Knock::None || self != s_knocked)
            return;

        DWORD const time = GetTickCount();
        DWORD const frame = time - s_knockSeenAt;
        s_knockSeenAt = time;
        if (IsPaused(self))
        {
            s_knockedAt += frame;
            s_landedAt += frame;
            return;
        }

        uint32_t const flags = Read<uint32_t>(self, OFF_MOVEMENT_FLAGS);
        if (flags != s_lastFlags)
        {
            auto* controller = *reinterpret_cast<uint8_t**>(VAR_INPUT_CONTROLLER);
            Log("knocked mover flags %#x, keys %#x", flags, controller ? Read<uint32_t>(controller, OFF_INPUT_KEYS) : 0);
            s_lastFlags = flags;
        }

        if (s_knock == Knock::InTheAir)
        {
            if (time - s_knockedAt > KNOCKBACK_GIVE_UP_MS)
            {
                Log("never landed; given up");
                s_knock = Knock::None;
            }
            else if (!(flags & MOVEFLAG_FALLING))
            {
                s_knock = Knock::Landed;
                s_landedAt = time;
            }
            return;
        }

        if (time - s_landedAt > LANDED_WATCH_MS)
        {
            Log("nothing to press again; done");
            s_knock = Knock::None;
            return;
        }
        if (flags & (MOVEFLAG_STOP_ON_LAND | MOVEFLAG_FALLING | MOVEFLAG_ROOT))
            return;
        if (ResumeHeldMovement(self, flags, *reinterpret_cast<uint32_t*>(VAR_INPUT_TIME)))
            s_knock = Knock::None;
    }

    uint32_t __fastcall OnApplyPendingStop(void* self, void* edx)
    {
        bool const landing = uintptr_t(_ReturnAddress()) == LANDING_STOP_RETURN;
        bool const stopOnLand = (Read<uint32_t>(self, OFF_MOVEMENT_FLAGS) & MOVEFLAG_STOP_ON_LAND) != 0;
        uint32_t const stopped = s_applyPendingStop(self, edx);
        if (landing && stopOnLand && IsActiveMover(self))
        {
            Log("knockback landed: stopped %#x", stopped);
            if (ResumeHeldMovement(self, Read<uint32_t>(self, OFF_MOVEMENT_FLAGS), MoveTimeNow()) && self == s_knocked)
                s_knock = Knock::None;
        }
        return stopped;
    }

    void __fastcall OnApplyKnockback(void* self, void* edx, uint32_t a, uint32_t b, uint32_t c)
    {
        s_applyKnockback(self, edx, a, b, c);
        if (!IsHero(self))
            return;
        s_knocked = self;
        s_knockedAt = GetTickCount();
        s_knockSeenAt = s_knockedAt;
        s_knock = Knock::InTheAir;
        s_lastFlags = 0;
        float const speed = AsFloat(b), lift = AsFloat(c);
        if (lift == 0.0f && speed >= DASH_SPEED_MIN)
        {
            s_airDash = self;
            s_airDashLeftMs = DASH_AIR_MS;
            Log("knockback on the hero: an air dash at %.1f yd/s", speed);
        }
        else
            Log("knockback on the hero");
    }

    void AirDashStep(void* self, void* edx, uint32_t time, uint32_t ms)
    {
        if (!(Read<uint32_t>(self, OFF_MOVEMENT_FLAGS) & MOVEFLAG_FALLING))
        {
            Log("air dash: landed after %u ms", DASH_AIR_MS - s_airDashLeftMs);
            s_airDash = nullptr;
            s_moverStep(self, edx, time, ms);
            return;
        }
        Write<uint32_t>(self, OFF_FALL_TIME, 0);
        s_moverStep(self, edx, time, ms);
        s_airDashLeftMs = ms < s_airDashLeftMs ? s_airDashLeftMs - ms : 0;
        if (s_airDashLeftMs)
            return;
        s_airDash = nullptr;
        float const speed = Read<float>(self, OFF_MOVE_SPEED), run = Read<float>(self, OFF_RUN_SPEED);
        if ((Read<uint32_t>(self, OFF_MOVEMENT_FLAGS) & MOVEFLAG_FALLING) && run > 0.0f && run < speed)
        {
            reinterpret_cast<RestartSegment_t>(FUN_RESTART_SEGMENT)(self, nullptr, 0);
            Write<float>(self, OFF_MOVE_SPEED, run);
        }
        Log("air dash: falls on at %.1f yd/s, flags %#x", Read<float>(self, OFF_MOVE_SPEED), Read<uint32_t>(self, OFF_MOVEMENT_FLAGS));
    }

    void __fastcall OnMoverStep(void* self, void* edx, uint32_t time, uint32_t ms)
    {
        bool const heldStill = IsPaused(self) && IsActiveMover(self);
        if (heldStill != s_heldStill && IsActiveMover(self))
        {
            uint32_t const flags = Read<uint32_t>(self, OFF_MOVEMENT_FLAGS);
            Log("%s: flags %#x, falling for %u ms", heldStill ? "held still" : "moving again", flags,
                (flags & MOVEFLAG_FALLING) ? Read<uint32_t>(self, OFF_FALL_TIME) : 0);
            s_heldStill = heldStill;
        }
        if (heldStill)
            return;
        if (s_airDash && self == s_airDash)
            AirDashStep(self, edx, time, ms);
        else
            s_moverStep(self, edx, time, ms);
    }

    int __fastcall OnSetRunSpeed(void* self, void* edx, float speed)
    {
        float const before = Read<float>(self, OFF_RUN_SPEED);
        int const changed = s_setRunSpeed(self, edx, speed);
        if (!changed || !IsActiveMover(self))
            return changed;

        uint32_t const flags = Read<uint32_t>(self, OFF_MOVEMENT_FLAGS);
        if (speed >= DASH_SPEED_MIN && before > 0.0f && before < DASH_SPEED_MIN)
        {
            Log("dash: run speed %.1f -> %.1f, flags %#x", before, speed, flags);
            if (!(flags & (MOVEFLAG_MOVES | MOVEFLAG_FALLING | MOVEFLAG_ROOT)))
                StartStandingDash(self);
        }
        else if (before >= DASH_SPEED_MIN && speed > 0.0f && speed < DASH_SPEED_MIN)
        {
            Log("dash over: run speed %.1f -> %.1f, flags %#x", before, speed, flags);
            float const moving = Read<float>(self, OFF_MOVE_SPEED);
            if ((flags & MOVEFLAG_FALLING) && moving > speed)
            {
                Write<float>(self, OFF_MOVE_SPEED, speed);
                Log("dash over in the air: %.1f -> %.1f yd/s", moving, speed);
            }
            if (s_dashStanding == self)
                EndStandingDash("its speed is back");
        }
        return changed;
    }

    int __fastcall OnJump(void* self, void* edx, int keyboard)
    {
        if (keyboard && IsPaused(self) && IsActiveMover(self))
        {
            Log("jump refused: held still");
            return 0;
        }
        return s_jump(self, edx, keyboard);
    }

    uintptr_t const THUNK_STREAM_OPEN   = 0x007FDA90;
    uintptr_t const THUNK_STREAM_PLAYEX = 0x007FDB5C;
    uintptr_t const THUNK_SET_VOLUME    = 0x007FDB86;
    uintptr_t const SLOT_STREAM_OPEN    = 0x007FF7FC;
    uintptr_t const SLOT_STREAM_PLAYEX  = 0x007FF79C;
    uintptr_t const SLOT_SET_VOLUME     = 0x007FF7B8;
    uintptr_t const SLOT_IS_PLAYING     = 0x007FF7A4;

    using StreamOpen_t = void*(__stdcall*)(char const* name, unsigned mode, int offset, int length);
    using StreamPlayEx_t = int(__stdcall*)(int channel, void* stream, void* dsp, signed char paused);
    using SetVolume_t = signed char(__stdcall*)(int channel, int volume);
    using IsPlaying_t = signed char(__stdcall*)(int channel);

    unsigned const FSOUND_LOADMEMORY = 0x8000;

    double const SAME_MOMENT_MS = 50.0;
    int const MAX_COPIES = 4;
    int const BUSY = 12;
    double const RECENT_MS = 1000.0;
    float const MIN_GAIN = 0.4f;
    DWORD const REPORT_MS = 10000;

    struct Voice
    {
        int channel;
        uint32_t sound;
        double started;
        float gain;
    };
    int const MAX_VOICES = 128;
    Voice s_voices[MAX_VOICES];
    int s_voiceCount = 0;

    std::unordered_map<void*, uint32_t> s_streamSounds;
    DWORD s_soundThread = 0;

    uint32_t s_played = 0;
    uint32_t s_droppedSameMoment = 0;
    uint32_t s_droppedTooMany = 0;
    DWORD s_reportedAt = 0;

    template <typename T>
    T Import(uintptr_t slot)
    {
        return *reinterpret_cast<T*>(slot);
    }

    double NowMs()
    {
        static LARGE_INTEGER frequency = [] { LARGE_INTEGER f; QueryPerformanceFrequency(&f); return f; }();
        LARGE_INTEGER now;
        QueryPerformanceCounter(&now);
        return double(now.QuadPart) * 1000.0 / double(frequency.QuadPart);
    }

    bool StartsWith(char const* text, char const* prefix)
    {
        return std::strncmp(text, prefix, std::strlen(prefix)) == 0;
    }

    uint32_t SoundOf(char const* name, unsigned mode)
    {
        if (!name || (mode & FSOUND_LOADMEMORY))
            return 0;
        char path[260];
        size_t length = 0;
        for (; name[length] && length + 1 < sizeof(path); ++length)
        {
            char c = name[length];
            if (c == '/')
                c = '\\';
            else if (c >= 'A' && c <= 'Z')
                c = char(c - 'A' + 'a');
            path[length] = c;
        }
        path[length] = '\0';
        if (StartsWith(path, "sound\\music\\") || StartsWith(path, "sound\\ambience\\"))
            return 0;
        uint32_t hash = 2166136261u;
        for (size_t i = 0; i < length; ++i)
            hash = (hash ^ uint8_t(path[i])) * 16777619u;
        return hash ? hash : 1;
    }

    void PruneVoices()
    {
        auto const isPlaying = Import<IsPlaying_t>(SLOT_IS_PLAYING);
        for (int i = 0; i < s_voiceCount;)
        {
            if (isPlaying(s_voices[i].channel))
                ++i;
            else
                s_voices[i] = s_voices[--s_voiceCount];
        }
    }

    void ReportSounds()
    {
        DWORD const time = GetTickCount();
        if (time - s_reportedAt < REPORT_MS)
            return;
        s_reportedAt = time;
        if (s_droppedSameMoment || s_droppedTooMany)
            Log("sounds: %u played, %u dropped as copies at the same moment, %u as too many copies", s_played,
                s_droppedSameMoment, s_droppedTooMany);
        s_played = s_droppedSameMoment = s_droppedTooMany = 0;
    }

    void* __stdcall OnStreamOpen(char const* name, unsigned mode, int offset, int length)
    {
        void* const stream = Import<StreamOpen_t>(SLOT_STREAM_OPEN)(name, mode, offset, length);
        if (!s_soundThread)
        {
            s_soundThread = GetCurrentThreadId();
            Log("sounds limited on thread %lu (first: %s)", s_soundThread, (mode & FSOUND_LOADMEMORY) || !name ? "from memory" : name);
        }
        if (stream && GetCurrentThreadId() == s_soundThread)
            s_streamSounds[stream] = SoundOf(name, mode);
        return stream;
    }

    int __stdcall OnStreamPlayEx(int channel, void* stream, void* dsp, signed char paused)
    {
        auto const play = Import<StreamPlayEx_t>(SLOT_STREAM_PLAYEX);
        if (GetCurrentThreadId() != s_soundThread)
            return play(channel, stream, dsp, paused);
        auto const found = s_streamSounds.find(stream);
        uint32_t const sound = found != s_streamSounds.end() ? found->second : 0;
        if (!sound)
            return play(channel, stream, dsp, paused);

        ReportSounds();
        PruneVoices();
        double const now = NowMs();
        int copies = 0, recent = 0;
        bool sameMoment = false;
        for (int i = 0; i < s_voiceCount; ++i)
        {
            Voice const& voice = s_voices[i];
            if (now - voice.started < RECENT_MS)
                ++recent;
            if (voice.sound != sound)
                continue;
            ++copies;
            if (now - voice.started < SAME_MOMENT_MS)
                sameMoment = true;
        }
        if (sameMoment)
        {
            ++s_droppedSameMoment;
            return -1;
        }
        if (copies >= MAX_COPIES)
        {
            ++s_droppedTooMany;
            return -1;
        }

        int const started = play(channel, stream, dsp, paused);
        if (started == -1)
            return started;
        ++s_played;

        float gain = 1.0f / std::sqrt(float(copies + 1));
        if (recent >= BUSY)
            gain *= std::sqrt(float(BUSY) / float(recent + 1));
        if (gain < MIN_GAIN)
            gain = MIN_GAIN;
        if (s_voiceCount == MAX_VOICES)
            s_voices[0] = s_voices[--s_voiceCount];
        s_voices[s_voiceCount++] = { started, sound, now, gain };
        if (gain < 1.0f)
            Import<SetVolume_t>(SLOT_SET_VOLUME)(started, int(255.0f * gain + 0.5f));
        return started;
    }

    signed char __stdcall OnSetVolume(int channel, int volume)
    {
        if (GetCurrentThreadId() == s_soundThread)
            for (int i = 0; i < s_voiceCount; ++i)
                if (s_voices[i].channel == channel)
                {
                    volume = int(float(volume) * s_voices[i].gain + 0.5f);
                    break;
                }
        return Import<SetVolume_t>(SLOT_SET_VOLUME)(channel, volume);
    }

    uintptr_t const COIN_SOUND_AT = 0x005DDF37;
    uint8_t const COIN_SOUND_CODE[] = { 0xB9, 0x84, 0x33, 0x84, 0x00, 0xE8, 0xEF, 0xA0, 0xE7, 0xFF };
    size_t const COIN_SOUND_CALL = 5;

    bool SilenceCoinSound()
    {
        auto* code = reinterpret_cast<uint8_t*>(COIN_SOUND_AT);
        if (std::memcmp(code, COIN_SOUND_CODE, sizeof(COIN_SOUND_CODE)) != 0)
            return false;
        DWORD protection;
        if (!VirtualProtect(code, sizeof(COIN_SOUND_CODE), PAGE_EXECUTE_READWRITE, &protection))
            return false;
        std::memset(code + COIN_SOUND_CALL, 0x90, sizeof(COIN_SOUND_CODE) - COIN_SOUND_CALL);
        VirtualProtect(code, sizeof(COIN_SOUND_CODE), protection, &protection);
        FlushInstructionCache(GetCurrentProcess(), code, sizeof(COIN_SOUND_CODE));
        return true;
    }

    uintptr_t const SOUND_BUS_CAPS_AT = 0x0087CE60;
    size_t const FILE_SOUND_BUS = 3;
    uint32_t const FILE_SOUND_STOCK_CAP = 2;
    uint32_t const FILE_SOUND_CAP = 8;

    bool RaiseFileSoundCap()
    {
        auto* cap = reinterpret_cast<uint32_t*>(SOUND_BUS_CAPS_AT) + FILE_SOUND_BUS;
        if (*cap != FILE_SOUND_STOCK_CAP)
            return false;
        DWORD protection;
        if (!VirtualProtect(cap, sizeof(*cap), PAGE_READWRITE, &protection))
            return false;
        *cap = FILE_SOUND_CAP;
        VirtualProtect(cap, sizeof(*cap), protection, &protection);
        return true;
    }

    uintptr_t const IDLE_CHECK_AT = 0x00482ED8;
    uint8_t const IDLE_CHECK_CODE[] = { 0x0F, 0x8C, 0xE1, 0x00, 0x00, 0x00 };
    uint8_t const IDLE_NEVER_CODE[] = { 0xE9, 0xE2, 0x00, 0x00, 0x00, 0x90 };

    bool DisableIdleAway()
    {
        auto* code = reinterpret_cast<uint8_t*>(IDLE_CHECK_AT);
        if (std::memcmp(code, IDLE_CHECK_CODE, sizeof(IDLE_CHECK_CODE)) != 0)
            return false;
        DWORD protection;
        if (!VirtualProtect(code, sizeof(IDLE_NEVER_CODE), PAGE_EXECUTE_READWRITE, &protection))
            return false;
        std::memcpy(code, IDLE_NEVER_CODE, sizeof(IDLE_NEVER_CODE));
        VirtualProtect(code, sizeof(IDLE_NEVER_CODE), protection, &protection);
        FlushInstructionCache(GetCurrentProcess(), code, sizeof(IDLE_NEVER_CODE));
        return true;
    }

    bool HookImportJump(uintptr_t thunk, uintptr_t slot, void* detour)
    {
        auto* code = reinterpret_cast<uint8_t*>(thunk);
        uint8_t expected[6] = { 0xFF, 0x25 };
        uint32_t const slot32 = uint32_t(slot);
        std::memcpy(expected + 2, &slot32, 4);
        if (std::memcmp(code, expected, sizeof(expected)) != 0)
            return false;

        DWORD protection;
        if (!VirtualProtect(code, sizeof(expected), PAGE_EXECUTE_READWRITE, &protection))
            return false;
        int32_t const jump = int32_t(uintptr_t(detour)) - int32_t(thunk + 5);
        code[0] = 0xE9;
        std::memcpy(code + 1, &jump, 4);
        code[5] = 0x90;
        VirtualProtect(code, sizeof(expected), protection, &protection);
        FlushInstructionCache(GetCurrentProcess(), code, sizeof(expected));
        return true;
    }

    void* HookBytes(uintptr_t target, uint8_t const* expected, size_t stolen, void* detour)
    {
        auto* code = reinterpret_cast<uint8_t*>(target);
        if (std::memcmp(code, expected, stolen) != 0)
            return nullptr;

        auto* trampoline = static_cast<uint8_t*>(VirtualAlloc(nullptr, 32, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE));
        if (!trampoline)
            return nullptr;
        std::memcpy(trampoline, code, stolen);
        trampoline[stolen] = 0xE9;
        int32_t const back = int32_t(target + stolen) - int32_t(uintptr_t(trampoline) + stolen + 5);
        std::memcpy(trampoline + stolen + 1, &back, 4);

        DWORD protection;
        if (!VirtualProtect(code, stolen, PAGE_EXECUTE_READWRITE, &protection))
        {
            VirtualFree(trampoline, 0, MEM_RELEASE);
            return nullptr;
        }
        int32_t const jump = int32_t(uintptr_t(detour)) - int32_t(target + 5);
        code[0] = 0xE9;
        std::memcpy(code + 1, &jump, 4);
        for (size_t i = 5; i < stolen; ++i)
            code[i] = 0x90;
        VirtualProtect(code, stolen, protection, &protection);
        FlushInstructionCache(GetCurrentProcess(), code, stolen);
        return trampoline;
    }

    void* Hook(uintptr_t target, uint8_t const* expected, void* detour)
    {
        return HookBytes(target, expected, STOLEN_BYTES, detour);
    }
}

namespace
{
    constexpr wchar_t GAME_TITLE[] = L"Hordes of Azeroth";
    constexpr wchar_t CLIENT_TITLE[] = L"World of Warcraft";
    constexpr DWORD TITLE_POLL_MS = 250;
    constexpr int TITLE_POLL_LIMIT = 240;

    BOOL CALLBACK RetitleWindow(HWND window, LPARAM renamed)
    {
        DWORD owner = 0;
        GetWindowThreadProcessId(window, &owner);
        if (owner != GetCurrentProcessId())
            return TRUE;
        wchar_t title[64] = {};
        GetWindowTextW(window, title, 64);
        if (wcscmp(title, CLIENT_TITLE) != 0)
            return TRUE;
        SetWindowTextW(window, GAME_TITLE);
        *reinterpret_cast<HWND*>(renamed) = window;
        return FALSE;
    }

    DWORD WINAPI RetitleGameWindow(LPVOID)
    {
        for (int attempt = 0; attempt < TITLE_POLL_LIMIT; ++attempt)
        {
            HWND renamed = nullptr;
            EnumWindows(&RetitleWindow, reinterpret_cast<LPARAM>(&renamed));
            if (renamed)
            {
                StartController(renamed);
                StartBorderless(renamed);
                return 0;
            }
            Sleep(TITLE_POLL_MS);
        }
        return 0;
    }
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason != DLL_PROCESS_ATTACH)
        return TRUE;
    DisableThreadLibraryCalls(instance);
    PrepareBorderless();

    s_moverUpdate = reinterpret_cast<MoverUpdate_t>(Hook(FUN_MOVER_UPDATE, MOVER_UPDATE_START, reinterpret_cast<void*>(&OnMoverUpdate)));
    if (s_moverUpdate)
        s_applyKnockback = reinterpret_cast<ApplyKnockback_t>(Hook(FUN_APPLY_KNOCKBACK, APPLY_KNOCKBACK_START, reinterpret_cast<void*>(&OnApplyKnockback)));
    s_moverStep = reinterpret_cast<MoverStep_t>(Hook(FUN_MOVER_STEP, MOVER_STEP_START, reinterpret_cast<void*>(&OnMoverStep)));
    s_jump = reinterpret_cast<Jump_t>(Hook(FUN_JUMP, JUMP_START, reinterpret_cast<void*>(&OnJump)));
    s_applyPendingStop = reinterpret_cast<ApplyPendingStop_t>(Hook(FUN_APPLY_PENDING_STOP, APPLY_PENDING_STOP_START,
        reinterpret_cast<void*>(&OnApplyPendingStop)));
    s_setRunSpeed = reinterpret_cast<SetRunSpeed_t>(Hook(FUN_SET_RUN_SPEED, SET_RUN_SPEED_START, reinterpret_cast<void*>(&OnSetRunSpeed)));
    Log("loaded: mover update hook %s, knockback hook %s, mover step hook %s, jump hook %s, landing stop hook %s, run speed hook %s",
        s_moverUpdate ? "on" : "OFF", s_applyKnockback ? "on" : "OFF", s_moverStep ? "on" : "OFF", s_jump ? "on" : "OFF",
        s_applyPendingStop ? "on" : "OFF", s_setRunSpeed ? "on" : "OFF");

    s_sceneAdvance = reinterpret_cast<SceneAdvance_t>(HookBytes(FUN_SCENE_ADVANCE, SCENE_ADVANCE_START, sizeof(SCENE_ADVANCE_START),
        reinterpret_cast<void*>(&OnSceneAdvance)));
    Log("world animation holds during pauses: %s", s_sceneAdvance ? "on" : "OFF");

    s_placeScreenRect = reinterpret_cast<PlaceScreenRect_t>(Hook(FUN_PLACE_SCREEN_RECT, PLACE_SCREEN_RECT_START,
        reinterpret_cast<void*>(&OnPlaceScreenRect)));
    Log("damage numbers stack in place: %s", s_placeScreenRect ? "on" : "OFF");

    bool const sounds = HookImportJump(THUNK_STREAM_OPEN, SLOT_STREAM_OPEN, reinterpret_cast<void*>(&OnStreamOpen)) &&
        HookImportJump(THUNK_SET_VOLUME, SLOT_SET_VOLUME, reinterpret_cast<void*>(&OnSetVolume)) &&
        HookImportJump(THUNK_STREAM_PLAYEX, SLOT_STREAM_PLAYEX, reinterpret_cast<void*>(&OnStreamPlayEx));
    Log("sound limit %s, coin sound on money %s", sounds ? "on" : "OFF", SilenceCoinSound() ? "off" : "ON");
    Log("idle away and sit %s", DisableIdleAway() ? "off" : "ON");
    Log("addon sound files at once: %s", RaiseFileSoundCap() ? "8" : "2 (stock)");

    if (HANDLE retitle = CreateThread(nullptr, 0, &RetitleGameWindow, nullptr, 0, nullptr))
        CloseHandle(retitle);
    return TRUE;
}
