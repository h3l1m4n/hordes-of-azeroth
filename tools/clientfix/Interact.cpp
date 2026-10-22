#include "ClientFix.h"
#include "ClientLua.h"

#include <cmath>
#include <cstdint>
#include <map>
#include <iterator>
#include <set>
#include <string>

namespace
{
    namespace Lua = ClientLua;

    uintptr_t const VAR_OBJECT_MANAGER      = 0x00B41414;
    uintptr_t const VAR_GAME_STATE          = 0x00B4B2BC;
    uintptr_t const FUN_RESOLVE_UNIT_TOKEN  = 0x00515940;
    uintptr_t const FUN_UNIT_NAME_POSITION  = 0x00608640;
    uintptr_t const FUN_UNIT_RIGHT_CLICK    = 0x0060BEA0;
    uintptr_t const FUN_OBJECT_RIGHT_CLICK  = 0x005F8660;

    uint32_t const OFF_MANAGER_FIRST        = 0xAC;
    uint32_t const OFF_MANAGER_LINK         = 0xA4;
    uint32_t const OFF_GAME_STATE_CAMERA    = 0x65B8;
    uint32_t const OFF_OBJECT_DESCRIPTORS   = 0x08;
    uint32_t const OFF_OBJECT_TYPE          = 0x14;
    uint32_t const OFF_OBJECT_GUID          = 0x30;
    uint32_t const OFF_OBJECT_MODEL         = 0xD8;
    uint32_t const VTABLE_GET_POSITION      = 0x14;

    uint32_t const OFF_CAMERA_POSITION      = 0x08;
    uint32_t const OFF_CAMERA_MATRIX        = 0x14;
    uint32_t const OFF_CAMERA_NEAR          = 0x38;
    uint32_t const OFF_CAMERA_FOV           = 0x40;
    uint32_t const OFF_CAMERA_ASPECT        = 0x44;

    uint32_t const DESC_ENTRY               = 0x0C;
    uint32_t const DESC_SCALE               = 0x10;
    uint32_t const DESC_UNIT_HEALTH         = 0x58;
    uint32_t const DESC_UNIT_FLAGS          = 0xB8;
    uint32_t const DESC_UNIT_NPC_FLAGS      = 0x24C;
    uint32_t const DESC_OBJECT_FLAGS        = 0x24;
    uint32_t const DESC_OBJECT_STATE        = 0x38;
    uint32_t const DESC_OBJECT_POSITION     = 0x3C;
    uint32_t const DESC_OBJECT_TYPE         = 0x54;

    uint32_t const TYPE_UNIT                = 3;
    uint32_t const TYPE_GAMEOBJECT          = 5;
    uint32_t const UNIT_FLAG_NOT_SELECTABLE = 0x02000000;
    uint32_t const OBJECT_FLAG_IN_USE       = 0x01;
    uint32_t const OBJECT_FLAG_NO_INTERACT  = 0x10;
    uint32_t const OBJECT_STATE_ACTIVE      = 0;
    uint32_t const USABLE_OBJECT_TYPES[]    = { 2, 3, 10, 19 };

    float const UNIT_REACH                  = 5.4f;
    float const OBJECT_REACH                = 5.0f;
    float const MAX_HEIGHT_DIFFERENCE       = 4.0f;
    float const SWITCH_MARGIN               = 0.75f;
    float const OBJECT_HEAD_HEIGHT          = 1.6f;
    float const UNIT_HEAD_FALLBACK          = 2.2f;
    float const PLAYER_HEIGHT_SMOOTHING     = 0.02f;
    DWORD const PICK_INTERVAL_MS            = 100;
    DWORD const AUTO_USE_INTERVAL_MS        = 150;
    DWORD const AUTO_USE_RETRY_MS           = 4000;

    struct Vec3
    {
        float x, y, z;
    };

    struct Pick
    {
        uint64_t guid = 0;
        uint8_t* object = nullptr;
        bool unit = false;
        uint32_t entry = 0;
        uint32_t detail = 0;
        float distance = 0.0f;
    };

    Pick s_pick;
    DWORD s_pickedAt = 0;
    float s_playerHeight = 0.0f;
    std::map<uint32_t, float> s_objectReach;
    bool s_skipUnits = false;
    std::set<uint32_t> s_skipObjectEntries;
    std::set<uint32_t> s_skipObjectTypes;
    std::set<uint32_t> s_autoUseEntries;
    std::map<uint64_t, DWORD> s_autoUsedAt;
    DWORD s_autoCheckedAt = 0;

    using ResolveUnitToken_t = uint8_t*(__fastcall*)(char const* token);
    using GetPosition_t = Vec3*(__thiscall*)(void* object, Vec3* out);
    using UnitNamePosition_t = Vec3*(__thiscall*)(void* unit, Vec3* out);
    using RightClick_t = void(__thiscall*)(void* object, int autoLoot);

    template <typename T>
    T Read(uint8_t const* base, uint32_t offset)
    {
        return *reinterpret_cast<T const*>(base + offset);
    }

    uint8_t* Descriptors(uint8_t* object)
    {
        return Read<uint8_t*>(object, OFF_OBJECT_DESCRIPTORS);
    }

    Vec3 Position(uint8_t* object)
    {
        auto const getPosition = reinterpret_cast<GetPosition_t>(Read<void**>(object, 0)[VTABLE_GET_POSITION / sizeof(void*)]);
        Vec3 out = {};
        Vec3 const* result = getPosition(object, &out);
        return result ? *result : out;
    }

    float Distance(Vec3 const& a, Vec3 const& b)
    {
        float const dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
        return std::sqrt(dx * dx + dy * dy + dz * dz);
    }

    uint8_t* ActiveCamera()
    {
        auto* state = *reinterpret_cast<uint8_t**>(VAR_GAME_STATE);
        return state ? Read<uint8_t*>(state, OFF_GAME_STATE_CAMERA) : nullptr;
    }

    uint8_t* Player()
    {
        return reinterpret_cast<ResolveUnitToken_t>(FUN_RESOLVE_UNIT_TOKEN)("player");
    }

    bool IsUsableObjectType(uint32_t type)
    {
        for (uint32_t usable : USABLE_OBJECT_TYPES)
            if (usable == type)
                return true;
        return false;
    }

    bool ReadCandidate(uint8_t* object, Vec3 const& from, Pick& out)
    {
        uint8_t* descriptors = Descriptors(object);
        if (!descriptors)
            return false;
        uint32_t const type = Read<uint32_t>(object, OFF_OBJECT_TYPE);
        Vec3 position;
        float reach;
        if (type == TYPE_UNIT)
        {
            uint32_t const npcFlags = Read<uint32_t>(descriptors, DESC_UNIT_NPC_FLAGS);
            if (!npcFlags || Read<uint32_t>(descriptors, DESC_UNIT_HEALTH) == 0 ||
                (Read<uint32_t>(descriptors, DESC_UNIT_FLAGS) & UNIT_FLAG_NOT_SELECTABLE))
                return false;
            position = Position(object);
            reach = UNIT_REACH;
            out.unit = true;
            out.detail = npcFlags;
        }
        else if (type == TYPE_GAMEOBJECT)
        {
            uint32_t const objectType = Read<uint32_t>(descriptors, DESC_OBJECT_TYPE);
            uint32_t const flags = Read<uint32_t>(descriptors, DESC_OBJECT_FLAGS);
            if (!IsUsableObjectType(objectType) || (flags & (OBJECT_FLAG_IN_USE | OBJECT_FLAG_NO_INTERACT)) ||
                Read<uint32_t>(descriptors, DESC_OBJECT_STATE) == OBJECT_STATE_ACTIVE)
                return false;
            position = Read<Vec3>(descriptors, DESC_OBJECT_POSITION);
            auto const custom = s_objectReach.find(Read<uint32_t>(descriptors, DESC_ENTRY));
            reach = custom != s_objectReach.end() ? custom->second : OBJECT_REACH;
            out.unit = false;
            out.detail = objectType;
        }
        else
            return false;

        if (std::fabs(position.z - from.z) > MAX_HEIGHT_DIFFERENCE)
            return false;
        out.distance = Distance(position, from);
        if (out.distance > reach)
            return false;
        out.object = object;
        out.guid = Read<uint64_t>(object, OFF_OBJECT_GUID);
        out.entry = Read<uint32_t>(descriptors, DESC_ENTRY);
        return true;
    }

    template <typename Visit>
    void ForEachObject(Visit visit)
    {
        uint8_t* manager = *reinterpret_cast<uint8_t**>(VAR_OBJECT_MANAGER);
        if (!manager)
            return;
        uint32_t const link = Read<uint32_t>(manager, OFF_MANAGER_LINK);
        auto* object = Read<uint8_t*>(manager, OFF_MANAGER_FIRST);
        while (object && !(reinterpret_cast<uintptr_t>(object) & 1))
        {
            if (!visit(object))
                return;
            object = Read<uint8_t*>(object, link + 4);
        }
    }

    uint8_t* FindObject(uint64_t guid)
    {
        uint8_t* found = nullptr;
        ForEachObject([&](uint8_t* object) {
            if (Read<uint64_t>(object, OFF_OBJECT_GUID) != guid)
                return true;
            found = object;
            return false;
        });
        return found;
    }

    bool IsSkipped(Pick const& pick)
    {
        if (pick.unit)
            return s_skipUnits;
        return s_skipObjectEntries.count(pick.entry) || s_skipObjectTypes.count(pick.detail);
    }

    void Repick(uint8_t* player)
    {
        Vec3 const from = Position(player);
        Pick best;
        Pick kept;
        ForEachObject([&](uint8_t* object) {
            if (object == player)
                return true;
            Pick candidate;
            if (!ReadCandidate(object, from, candidate) || IsSkipped(candidate))
                return true;
            if (candidate.guid == s_pick.guid)
                kept = candidate;
            if (!best.object || candidate.distance < best.distance)
                best = candidate;
            return true;
        });
        if (kept.object && best.distance > kept.distance - SWITCH_MARGIN)
            best = kept;
        if (best.guid != s_pick.guid && best.object)
            Log("interact: picked %s %u at %.1f yd", best.unit ? "unit" : "object", best.entry, best.distance);
        s_pick = best;
    }

    bool Project(uint8_t* camera, Vec3 const& world, float& x, float& y)
    {
        Vec3 const eye = Read<Vec3>(camera, OFF_CAMERA_POSITION);
        float const* m = reinterpret_cast<float const*>(camera + OFF_CAMERA_MATRIX);
        float const dx = world.x - eye.x, dy = world.y - eye.y, dz = world.z - eye.z;
        float const forward = dx * m[0] + dy * m[1] + dz * m[2];
        float const left = dx * m[3] + dy * m[4] + dz * m[5];
        float const up = dx * m[6] + dy * m[7] + dz * m[8];
        float const aspect = Read<float>(camera, OFF_CAMERA_ASPECT);
        float const fov = Read<float>(camera, OFF_CAMERA_FOV);
        if (forward <= Read<float>(camera, OFF_CAMERA_NEAR) || aspect <= 0.0f || fov <= 0.0f)
            return false;
        float const halfTan = std::tan(fov / std::sqrt(aspect * aspect + 1.0f) * 0.5f);
        x = 0.5f + 0.5f * (-left / forward) / (aspect * halfTan);
        y = 0.5f + 0.5f * (up / forward) / halfTan;
        return true;
    }

    Vec3 Feet(Pick const& pick)
    {
        return pick.unit ? Position(pick.object) : Read<Vec3>(Descriptors(pick.object), DESC_OBJECT_POSITION);
    }

    float Scale(Pick const& pick)
    {
        float const scale = Read<float>(Descriptors(pick.object), DESC_SCALE);
        return scale > 0.05f ? scale : 1.0f;
    }

    Vec3 Head(Pick const& pick, Vec3 const& feet)
    {
        if (pick.unit && Read<void*>(pick.object, OFF_OBJECT_MODEL))
        {
            Vec3 out = feet;
            Vec3 const* result = reinterpret_cast<UnitNamePosition_t>(FUN_UNIT_NAME_POSITION)(pick.object, &out);
            return result ? *result : out;
        }
        float const height = pick.unit ? UNIT_HEAD_FALLBACK : OBJECT_HEAD_HEIGHT * Scale(pick);
        return { feet.x, feet.y, feet.z + height };
    }

    bool CurrentPick(uint8_t* player)
    {
        DWORD const now = GetTickCount();
        if (now - s_pickedAt >= PICK_INTERVAL_MS || (s_pick.guid && FindObject(s_pick.guid) != s_pick.object))
        {
            s_pickedAt = now;
            Repick(player);
        }
        return s_pick.object != nullptr;
    }

    int __fastcall Lua_GetInteract(void* L)
    {
        uint8_t* player = Player();
        uint8_t* camera = player ? ActiveCamera() : nullptr;
        if (!camera || !CurrentPick(player))
        {
            Lua::pushnil(L);
            return 1;
        }
        Vec3 const feet = Feet(s_pick);
        Vec3 const head = Head(s_pick, feet);
        float headX, headY;
        if (!Project(camera, head, headX, headY))
        {
            Lua::pushnil(L);
            return 1;
        }
        Lua::pushstring(L, s_pick.unit ? "unit" : "object");
        Lua::pushnumber(L, double(s_pick.entry));
        Lua::pushnumber(L, double(s_pick.detail));
        Lua::pushnumber(L, double(headX));
        Lua::pushnumber(L, double(headY));
        Lua::pushnumber(L, double(s_pick.distance));
        return 6;
    }

    int __fastcall Lua_GetPlayerHead(void* L)
    {
        uint8_t* player = Player();
        uint8_t* camera = player ? ActiveCamera() : nullptr;
        if (!camera)
        {
            Lua::pushnil(L);
            return 1;
        }
        Pick self;
        self.object = player;
        self.unit = true;
        Vec3 const feet = Position(player);
        float const height = Head(self, feet).z - feet.z;
        if (height > 0.0f)
            s_playerHeight = s_playerHeight > 0.0f ? s_playerHeight + (height - s_playerHeight) * PLAYER_HEIGHT_SMOOTHING : height;
        Vec3 const head = { feet.x, feet.y, feet.z + (s_playerHeight > 0.0f ? s_playerHeight : UNIT_HEAD_FALLBACK) };
        float x, y;
        if (!Project(camera, head, x, y))
        {
            Lua::pushnil(L);
            return 1;
        }
        Lua::pushnumber(L, double(x));
        Lua::pushnumber(L, double(y));
        return 2;
    }

    int __fastcall Lua_SetObjectReach(void* L)
    {
        uint32_t const entry = uint32_t(Lua::tonumber(L, 1));
        float const yards = float(Lua::tonumber(L, 2));
        if (yards > 0.0f)
            s_objectReach[entry] = yards;
        else
            s_objectReach.erase(entry);
        return 0;
    }

    void SetMember(std::set<uint32_t>& members, uint32_t id, bool member)
    {
        if (member)
            members.insert(id);
        else
            members.erase(id);
    }

    int __fastcall Lua_SetInteractSkip(void* L)
    {
        char const* kind = Lua::tostring(L, 1);
        uint32_t const id = uint32_t(Lua::tonumber(L, 2));
        bool const skip = Lua::toboolean(L, 3) != 0;
        std::string const which = kind ? kind : "";
        if (which == "unit")
            s_skipUnits = skip;
        else if (which == "object")
            SetMember(s_skipObjectEntries, id, skip);
        else if (which == "type")
            SetMember(s_skipObjectTypes, id, skip);
        s_pickedAt = 0;
        return 0;
    }

    int __fastcall Lua_SetAutoUse(void* L)
    {
        SetMember(s_autoUseEntries, uint32_t(Lua::tonumber(L, 1)), Lua::toboolean(L, 2) != 0);
        return 0;
    }

    void ForgetOldAutoUses(DWORD now)
    {
        for (auto used = s_autoUsedAt.begin(); used != s_autoUsedAt.end();)
            used = now - used->second >= AUTO_USE_RETRY_MS ? s_autoUsedAt.erase(used) : std::next(used);
    }

    int __fastcall Lua_AutoUse(void* L)
    {
        DWORD const now = GetTickCount();
        uint8_t* player = Player();
        if (!player || s_autoUseEntries.empty() || now - s_autoCheckedAt < AUTO_USE_INTERVAL_MS)
        {
            Lua::pushboolean(L, 0);
            return 1;
        }
        s_autoCheckedAt = now;
        ForgetOldAutoUses(now);
        Vec3 const from = Position(player);
        Pick nearest;
        ForEachObject([&](uint8_t* object) {
            Pick candidate;
            if (object == player || !ReadCandidate(object, from, candidate) || candidate.unit ||
                !s_autoUseEntries.count(candidate.entry) || s_autoUsedAt.count(candidate.guid))
                return true;
            if (!nearest.object || candidate.distance < nearest.distance)
                nearest = candidate;
            return true;
        });
        if (!nearest.object)
        {
            Lua::pushboolean(L, 0);
            return 1;
        }
        s_autoUsedAt[nearest.guid] = now;
        Log("interact: auto-using object %u at %.1f yd", nearest.entry, nearest.distance);
        reinterpret_cast<RightClick_t>(FUN_OBJECT_RIGHT_CLICK)(nearest.object, 0);
        Lua::pushboolean(L, 1);
        return 1;
    }

    int __fastcall Lua_Interact(void* L)
    {
        uint8_t* player = Player();
        if (!player || !CurrentPick(player))
        {
            Lua::pushboolean(L, 0);
            return 1;
        }
        auto const rightClick = reinterpret_cast<RightClick_t>(s_pick.unit ? FUN_UNIT_RIGHT_CLICK : FUN_OBJECT_RIGHT_CLICK);
        Log("interact: using %s %u", s_pick.unit ? "unit" : "object", s_pick.entry);
        rightClick(s_pick.object, 0);
        Lua::pushboolean(L, 1);
        return 1;
    }
}

void RegisterInteractFunctions()
{
    s_pick = Pick();
    s_playerHeight = 0.0f;
    s_skipUnits = false;
    s_skipObjectEntries.clear();
    s_skipObjectTypes.clear();
    s_autoUseEntries.clear();
    s_autoUsedAt.clear();
    Lua::Register("HoAController_GetInteract", &Lua_GetInteract);
    Lua::Register("HoAController_Interact", &Lua_Interact);
    Lua::Register("HoAController_SetObjectReach", &Lua_SetObjectReach);
    Lua::Register("HoAController_GetPlayerHead", &Lua_GetPlayerHead);
    Lua::Register("HoAController_SetInteractSkip", &Lua_SetInteractSkip);
    Lua::Register("HoAController_SetAutoUse", &Lua_SetAutoUse);
    Lua::Register("HoAController_AutoUse", &Lua_AutoUse);
}
