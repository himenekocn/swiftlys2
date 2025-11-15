/************************************************************************************************
 * SwiftlyS2 Benchmark Module
 * Provides safe native functions for C# to C++ interop performance testing
 * All functions are designed to be lightweight and safe for repeated calls
 ************************************************************************************************/

#include <cstdint>
#include <mathlib/vector.h>
#include <public/Color.h>
#include <scripting/scripting.h>


// Pattern 1: void -> void
void Bridge_Benchmark_VoidToVoid() {}

// Pattern 2: void -> primitive types
bool Bridge_Benchmark_GetBool()
{
    return true;
}
int32_t Bridge_Benchmark_GetInt32()
{
    return 42;
}
uint32_t Bridge_Benchmark_GetUInt32()
{
    return 1337u;
}
int64_t Bridge_Benchmark_GetInt64()
{
    return 42000000LL;
}
uint64_t Bridge_Benchmark_GetUInt64()
{
    return 1337000000ULL;
}
float Bridge_Benchmark_GetFloat()
{
    return 3.14f;
}
double Bridge_Benchmark_GetDouble()
{
    return 3.14159;
}
void* Bridge_Benchmark_GetPtr()
{
    return reinterpret_cast<void*>(0x1337);
}

// Pattern 3: Single primitive -> same primitive
bool Bridge_Benchmark_BoolToBool(bool value)
{
    return !value;
}
int32_t Bridge_Benchmark_Int32ToInt32(int32_t value)
{
    return value + 1;
}
uint32_t Bridge_Benchmark_UInt32ToUInt32(uint32_t value)
{
    return value + 1;
}
int64_t Bridge_Benchmark_Int64ToInt64(int64_t value)
{
    return value + 1;
}
uint64_t Bridge_Benchmark_UInt64ToUInt64(uint64_t value)
{
    return value + 1;
}
float Bridge_Benchmark_FloatToFloat(float value)
{
    return value + 1.0f;
}
double Bridge_Benchmark_DoubleToDouble(double value)
{
    return value + 1.0;
}
void* Bridge_Benchmark_PtrToPtr(void* value)
{
    return value;
}

// Pattern 4: String (UTF-8 encoding test)
const char* Bridge_Benchmark_StringToString(const char* value)
{
    return "test";
}
void* Bridge_Benchmark_StringToPtr(const char* value)
{
    return reinterpret_cast<void*>(0xBEEF);
}

// Pattern 5: Multiple primitives (no string)
int32_t Bridge_Benchmark_MultiPrimitives(void* p1, int32_t i1, float f1, bool b1, uint64_t u1)
{
    return i1 + (int32_t)f1;
}

// Pattern 6: Multiple with 1 string
int32_t Bridge_Benchmark_MultiWithOneString(void* p1, const char* s1, void* p2, int32_t i1, float f1)
{
    return i1;
}

// Pattern 7: Multiple with 2 strings (heaviest encoding)
void Bridge_Benchmark_MultiWithTwoStrings(void* p1, const char* s1, void* p2, const char* s2, int32_t i1) {}

// Pattern 8: Complex types (vector, qangle, color)
Vector Bridge_Benchmark_VectorToVector(Vector value)
{
    return Vector(value.x + 1, value.y + 1, value.z + 1);
}
QAngle Bridge_Benchmark_QAngleToQAngle(QAngle value)
{
    return QAngle(value.x + 1, value.y + 1, value.z + 1);
}
Color Bridge_Benchmark_ColorToColor(Color value)
{
    return Color(255, 0, 0, 255);
}

// Pattern 9: Mixed complex + string
void Bridge_Benchmark_ComplexWithString(void* entity, Vector pos, const char* name, QAngle angle) {}

DEFINE_NATIVE("Benchmark.VoidToVoid", Bridge_Benchmark_VoidToVoid);
DEFINE_NATIVE("Benchmark.GetBool", Bridge_Benchmark_GetBool);
DEFINE_NATIVE("Benchmark.GetInt32", Bridge_Benchmark_GetInt32);
DEFINE_NATIVE("Benchmark.GetUInt32", Bridge_Benchmark_GetUInt32);
DEFINE_NATIVE("Benchmark.GetInt64", Bridge_Benchmark_GetInt64);
DEFINE_NATIVE("Benchmark.GetUInt64", Bridge_Benchmark_GetUInt64);
DEFINE_NATIVE("Benchmark.GetFloat", Bridge_Benchmark_GetFloat);
DEFINE_NATIVE("Benchmark.GetDouble", Bridge_Benchmark_GetDouble);
DEFINE_NATIVE("Benchmark.GetPtr", Bridge_Benchmark_GetPtr);
DEFINE_NATIVE("Benchmark.BoolToBool", Bridge_Benchmark_BoolToBool);
DEFINE_NATIVE("Benchmark.Int32ToInt32", Bridge_Benchmark_Int32ToInt32);
DEFINE_NATIVE("Benchmark.UInt32ToUInt32", Bridge_Benchmark_UInt32ToUInt32);
DEFINE_NATIVE("Benchmark.Int64ToInt64", Bridge_Benchmark_Int64ToInt64);
DEFINE_NATIVE("Benchmark.UInt64ToUInt64", Bridge_Benchmark_UInt64ToUInt64);
DEFINE_NATIVE("Benchmark.FloatToFloat", Bridge_Benchmark_FloatToFloat);
DEFINE_NATIVE("Benchmark.DoubleToDouble", Bridge_Benchmark_DoubleToDouble);
DEFINE_NATIVE("Benchmark.PtrToPtr", Bridge_Benchmark_PtrToPtr);
DEFINE_NATIVE("Benchmark.StringToString", Bridge_Benchmark_StringToString);
DEFINE_NATIVE("Benchmark.StringToPtr", Bridge_Benchmark_StringToPtr);
DEFINE_NATIVE("Benchmark.MultiPrimitives", Bridge_Benchmark_MultiPrimitives);
DEFINE_NATIVE("Benchmark.MultiWithOneString", Bridge_Benchmark_MultiWithOneString);
DEFINE_NATIVE("Benchmark.MultiWithTwoStrings", Bridge_Benchmark_MultiWithTwoStrings);
DEFINE_NATIVE("Benchmark.VectorToVector", Bridge_Benchmark_VectorToVector);
DEFINE_NATIVE("Benchmark.QAngleToQAngle", Bridge_Benchmark_QAngleToQAngle);
DEFINE_NATIVE("Benchmark.ColorToColor", Bridge_Benchmark_ColorToColor);
DEFINE_NATIVE("Benchmark.ComplexWithString", Bridge_Benchmark_ComplexWithString);