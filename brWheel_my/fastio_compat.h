#ifndef FASTIO_COMPAT_H
#define FASTIO_COMPAT_H

#include <Arduino.h>

// RP2040 doesn't ship with digitalWriteFast; fall back to standard calls.
#if defined(ARDUINO_ARCH_RP2040)
#define digitalWriteFast digitalWrite
#define pinModeFast pinMode
#define digitalReadFast digitalRead
#else
#include <digitalWriteFast.h>
#endif

#endif // FASTIO_COMPAT_H
