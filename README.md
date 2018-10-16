# Asmuth
A C# library for manipulating x86-64 instructions and other concepts of the architecture. The focus so far is on decoding machine code, but it may grow into implementing tools (disassembler? debugger?) or having a more symbolic representation. The motivation is simply to formalize the concepts in my mind by coding them up.

## Approach
Asmuth has a strong object model which favors immutability and minimizes both ambiguity and redundancy.Strong types are provided even for concepts as simple as an address size or a ModR/M byte. The instruction representation attempts to strike a good balance between being compact, supporting round-trips to its byte representation and providing a strong object model. Opcode encoding information is fully declarative.

## Opcode data sources
One difficulty of x86-64 is the vast number of opcodes in existence, and the fact that the only official references are human-readable but non-machine-friendly PDFs by processor vendors (despite of the work by my friend Félix Cloutier to process them into HTML).

### NASM
This project initially leveraged the NASM data files, which produced satisfyingly exhaustive opcode definitions but was complex due to their format, which is tailor-made for NASM's strings-to-bytes and bytes-to-strings operation.

### XED
I was later made aware of Intel's XED project and its data files, which are as close as there is to an official, machine-readable definition of the x86-64 instruction set. Their format, however, is that of a rule-based language meant to be used to generate code for an encoder or decoder. With some assumptions