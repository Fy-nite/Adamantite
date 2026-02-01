// Adamantite.cpp : Defines the entry point for the application.
//

#include "Adamantite.h"
#include <SDL.h>
#include "../Adamantite.core/Logging.hpp"

using namespace std;
using namespace Adamantite::core;

int main(int argc, char** argv)
{
    // Simple command-line parsing: --log-level <level>
    for (int i = 1; i < argc; ++i) {
        std::string arg = argv[i];
        if (arg == "--log-level" && i + 1 < argc) {
            SetLogLevel(ParseLogLevel(argv[++i]));
        }
    }

    Info("Hello Adamantite Core.");
    Warning("This is a warning message.");
    Error("This is an error message.");
    return 0;
}
