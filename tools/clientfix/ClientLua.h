#pragma once

#include <cstdint>

namespace ClientLua
{
    uintptr_t const VAR_LUA_STATE         = 0x00CEEF74;
    uintptr_t const FUN_REGISTER_FUNCTION = 0x00704120;

    int const GLOBALSINDEX = -10001;
    int const TNIL = 0;
    int const TFUNCTION = 6;

    using CFunction = int(__fastcall*)(void* L);
    using RegisterFunction_t = void(__fastcall*)(char const* name, CFunction func);

    using tonumber_t = double(__fastcall*)(void* L, int index);
    using toboolean_t = int(__fastcall*)(void* L, int index);
    using tostring_t = char const*(__fastcall*)(void* L, int index);
    using pushnumber_t = void(__fastcall*)(void* L, double n);
    using pushnil_t = void(__fastcall*)(void* L);
    using pushboolean_t = void(__fastcall*)(void* L, int b);
    using pushstring_t = void(__fastcall*)(void* L, char const* s);
    using gettable_t = void(__fastcall*)(void* L, int index);
    using type_t = int(__fastcall*)(void* L, int index);
    using gettop_t = int(__fastcall*)(void* L);
    using settop_t = void(__fastcall*)(void* L, int index);
    using pcall_t = int(__fastcall*)(void* L, int nargs, int nresults, int errfunc);

    inline auto const tonumber = reinterpret_cast<tonumber_t>(0x006F3620);
    inline auto const toboolean = reinterpret_cast<toboolean_t>(0x006F3660);
    inline auto const tostring = reinterpret_cast<tostring_t>(0x006F3690);
    inline auto const pushnumber = reinterpret_cast<pushnumber_t>(0x006F3810);
    inline auto const pushnil = reinterpret_cast<pushnil_t>(0x006F37F0);
    inline auto const pushboolean = reinterpret_cast<pushboolean_t>(0x006F39F0);
    inline auto const pushstring = reinterpret_cast<pushstring_t>(0x006F3890);
    inline auto const gettable = reinterpret_cast<gettable_t>(0x006F3A40);
    inline auto const type = reinterpret_cast<type_t>(0x006F3400);
    inline auto const gettop = reinterpret_cast<gettop_t>(0x006F3070);
    inline auto const settop = reinterpret_cast<settop_t>(0x006F3080);
    inline auto const pcall = reinterpret_cast<pcall_t>(0x006F41A0);

    inline void Register(char const* name, CFunction func)
    {
        reinterpret_cast<RegisterFunction_t>(FUN_REGISTER_FUNCTION)(name, func);
    }
}
