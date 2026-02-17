#ifndef _PLATFORM_H_
#define _PLATFORM_H_

// Compatibility shim for legacy Arduino core sources in this firmware tree.
// Newer AVR cores do not provide Platform.h, and including Arduino.h here pulls
// in the stock USBAPI declarations that conflict with the custom HID/USB stack.
// Keep this header minimal and USB-focused.

#include <inttypes.h>
#include <stddef.h>
#include <avr/io.h>
#include <avr/pgmspace.h>
#include <avr/eeprom.h>
#include <avr/interrupt.h>
#include <util/delay.h>

#include "Print.h"
#include "Stream.h"

// Pull in common Arduino core macros (delay, min/max, USB descriptor constants)
// without importing the core USBAPI declarations that conflict with this firmware.
#ifndef BRWHEEL_SUPPRESS_CORE_USBAPI
#define BRWHEEL_SUPPRESS_CORE_USBAPI
#define __USBAPI__
#include <Arduino.h>
#undef __USBAPI__
#undef BRWHEEL_SUPPRESS_CORE_USBAPI
#endif

#ifndef WEAK
#define WEAK __attribute__((weak))
#endif

#ifndef BRWHEEL_FIXED_TYPES_DEFINED
#define BRWHEEL_FIXED_TYPES_DEFINED
typedef int8_t s8;
typedef uint8_t u8;
typedef int8_t b8;
typedef int16_t s16;
typedef uint16_t u16;
typedef int32_t s32;
typedef uint32_t u32;
typedef float f32;
#endif

#endif
