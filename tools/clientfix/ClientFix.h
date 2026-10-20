#pragma once

#include <windows.h>

void Log(char const* format, ...);
void StartController(HWND window);
void RegisterInteractFunctions();
