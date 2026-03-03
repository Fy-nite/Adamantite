#pragma once

#include <string>
#include <vector>
#include <iostream>
#include <cstdint>

#ifdef _WIN32
#  include <windows.h>
#endif

namespace Adamantite::Util {

class DiagnosticsProvider {
public:
    enum class Level { Info, Warning, Error, Debug };

    static void Emit(Level level,
                     const std::string& header   = "",
                     const std::string& contents = "",
                     const std::vector<std::string>* callStack = nullptr)
    {
        bool useColor = EnableVirtualTerminal();
        std::string levelName  = LevelName(level);
        std::string levelColor = LevelColor(level);
        const char* Reset = "\x1b[0m";
        const char* Bold  = "\x1b[1m";
        const char* Gray  = "\x1b[90m";
        const char* Cyan  = "\x1b[36m";

        std::cerr << "\n";
        if (useColor) std::cerr << Bold << levelColor;
        std::cerr << "== " << levelName << " ==";
        if (useColor) std::cerr << Reset;
        if (!header.empty()) {
            std::cerr << " ";
            if (useColor) std::cerr << Bold;
            std::cerr << header;
            if (useColor) std::cerr << Reset;
        }
        std::cerr << "\n";

        if (!contents.empty()) {
            if (useColor) std::cerr << Gray;
            std::cerr << contents << "\n";
            if (useColor) std::cerr << Reset;
        }

        if (callStack && !callStack->empty()) {
            if (useColor) std::cerr << Bold << Cyan;
            std::cerr << "\nCall Stack (most recent call first):\n";
            if (useColor) std::cerr << Reset;
            for (size_t i = 0; i < callStack->size(); i++) {
                std::cerr << "  #" << i << " " << (*callStack)[i] << "\n";
            }
        }
    }

    static void Info(const std::string& header,
                     const std::string& contents = "",
                     const std::vector<std::string>* callStack = nullptr)
    { Emit(Level::Info, header, contents, callStack); }

    static void Warning(const std::string& header,
                        const std::string& contents = "",
                        const std::vector<std::string>* callStack = nullptr)
    { Emit(Level::Warning, header, contents, callStack); }

    static void Error(const std::string& header,
                      const std::string& contents = "",
                      const std::vector<std::string>* callStack = nullptr)
    { Emit(Level::Error, header, contents, callStack); }

    static void Debug(const std::string& header,
                      const std::string& contents = "",
                      const std::vector<std::string>* callStack = nullptr)
    { Emit(Level::Debug, header, contents, callStack); }

private:
    static std::string LevelName(Level level) {
        switch (level) {
            case Level::Info:    return "INFO";
            case Level::Warning: return "WARN";
            case Level::Error:   return "ERROR";
            case Level::Debug:   return "DEBUG";
            default:             return "LOG";
        }
    }

    static std::string LevelColor(Level level) {
        switch (level) {
            case Level::Info:    return "\x1b[36m"; // Cyan
            case Level::Warning: return "\x1b[33m"; // Yellow
            case Level::Error:   return "\x1b[31m"; // Red
            case Level::Debug:   return "\x1b[35m"; // Magenta
            default:             return "\x1b[90m"; // Gray
        }
    }

    static bool EnableVirtualTerminal() {
#ifdef _WIN32
        HANDLE h = GetStdHandle(STD_ERROR_HANDLE);
        if (!h || h == INVALID_HANDLE_VALUE) return false;
        DWORD mode = 0;
        if (!GetConsoleMode(h, &mode)) return false;
        mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
        return SetConsoleMode(h, mode) != 0;
#else
        return true;
#endif
    }
};

} // namespace Adamantite::Util
