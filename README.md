# JoshaParity

Rudamentary Parity Checker for Beatsaber Maps written in C#

Uses dictionaries and math to attempt to figure out how a map is played assuming the
rule of absolute parity. Logic for parity can be hotswapped by creating a new checker inheriting from IParityMethod.
Currently Used by JindoRankTool for the previewing functionality


Notice:
- Currently reworking so can be used as a library

Usage:
- As of current, you will need visual studio, and the V2 Format Map you wish to check, then supply its location in program.cs

Thanks to:
- Jindo, for allowing me to backport the v2 to v3 conversion and v3 format support from JindoRankTool

Known Issues:
- Dodgy dot bomb reset detection / no detection at all
- Some combinations of dots in windows and sliders can cause the rotation for the swing to be upside down, though
  the "parity" itself should be correct.
- V3 Features not supported (Chains, Arcs)
