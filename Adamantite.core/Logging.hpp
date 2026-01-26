#pragma once

#include <string>
#include <atomic>

namespace Adamantite::core {

enum class LogLevel : int {
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    None = 5
};

// Inline atomic storage for the current log level. Header-only definition (C++17+).
inline std::atomic<int> g_log_level{static_cast<int>(LogLevel::Info)};

inline void SetLogLevel(LogLevel level) { g_log_level.store(static_cast<int>(level)); }
inline LogLevel GetLogLevel() { return static_cast<LogLevel>(g_log_level.load()); }

// Parse textual level (case-insensitive). Returns LogLevel::Info on unknown input.
LogLevel ParseLogLevel(const std::string& str);

// Logging functions. They check the current global level and print when appropriate.
void Info(const std::string& message);
void Warning(const std::string& message);
void Error(const std::string& message);

} // namespace Adamantite::core
