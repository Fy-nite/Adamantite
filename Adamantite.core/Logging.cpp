// Adamantite.cpp : Defines the entry point for the application.
//

#include "Logging.hpp"
#include <iostream>
#include <algorithm>

using namespace std;

namespace Adamantite::core {

LogLevel ParseLogLevel(const std::string& str) {
    std::string s = str;
    std::transform(s.begin(), s.end(), s.begin(), [](unsigned char c){ return std::tolower(c); });
    if (s == "trace") return LogLevel::Trace;
    if (s == "debug") return LogLevel::Debug;
    if (s == "info") return LogLevel::Info;
    if (s == "warning" || s == "warn") return LogLevel::Warning;
    if (s == "error") return LogLevel::Error;
    if (s == "none") return LogLevel::None;
    return LogLevel::Info;
}

static bool should_log(LogLevel messageLevel) {
    return static_cast<int>(messageLevel) >= static_cast<int>(GetLogLevel());
}

void Info(const std::string& message) {
    if (!should_log(LogLevel::Info)) return;
    std::cout << "\x1B[32m[INFO]: " << message << "\x1B[0m" << std::endl;
}

void Warning(const std::string& message) {
    if (!should_log(LogLevel::Warning)) return;
    std::cout << "\x1B[33m[WARNING]: " << message << "\x1B[0m" << std::endl;
}

void Error(const std::string& message) {
    if (!should_log(LogLevel::Error)) return;
    std::cerr << "\x1B[31m[ERROR]: " << message << "\x1B[0m" << std::endl;
}

} // namespace Adamantite::core
