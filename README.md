# JoshaParity

Rudamentary Parity Checker for Beatsaber Maps written in C#

Uses dictionaries and math to attempt to figure out how a map is played assuming the
rule of absolute parity. Logic for parity can be hotswapped by creating a new checker inheriting from IParityMethod.

Notice:
- Currently reworking so can be used as a library
- V3 map format support is coming, though V3 features such as chains and arcs are currently not supported

Currently Used by JindoRankTool for the previewing functionality

Usage:
- As of current, you will need visual studio, and the V2 Format Map you wish to check, then supply its location in program.cs


Recent Changes:
- Brought back all new functionality and refactoring from JindoRankTool version where I was updating this

Known Issues:
- Dodgy dot bomb reset detection / no detection at all
- Some combinations of dots in windows and sliders can cause the rotation for the swing to be upside down, though
  the "parity" itself should be correct.
- Code base is a bit messy, working on refactoring it
- V3 Map Format and Features not supported (Chains, Arcs)
