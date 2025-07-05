# Telnet Encoding Configuration

## Overview

The Blackboard BBS supports multiple text encodings for telnet connections to ensure proper display of ANSI art and text alignment. This is crucial for maintaining the authentic BBS experience.

## Encoding Options

### ASCII (Default)
- **Use Case**: Best for most modern telnet clients
- **Pros**: 
  - Reliable character alignment for ANSI art
  - Single-byte encoding prevents positioning issues
  - Works with all telnet clients
- **Cons**: 
  - Limited to basic ASCII characters (0-127)
  - No extended character support

### UTF-8
- **Use Case**: When you need Unicode support
- **Pros**:
  - Supports all Unicode characters
  - Modern standard
- **Cons**:
  - Multi-byte encoding can cause ANSI art misalignment
  - Some older telnet clients may not handle properly

### CP437 (IBM Code Page 437)
- **Use Case**: Authentic retro BBS experience
- **Pros**:
  - Original IBM PC character set
  - Includes box-drawing characters and extended ASCII
  - Perfect for classic BBS ANSI art
  - Single-byte encoding maintains alignment
- **Cons**:
  - Limited to CP437 character set
  - May not display properly on all modern terminals

## Configuration

In your `blackboard.yml` file:

```yaml
network:
  telnetBindAddress: "0.0.0.0"
  telnetPort: 2323
  maxConcurrentConnections: 10
  connectionTimeoutSeconds: 300
  telnetEncoding: "ASCII"  # Options: ASCII, UTF-8, CP437
```

## Recommendations

### For Production BBS Systems
- **Use ASCII**: Best compatibility and ANSI art alignment
- **Use CP437**: If you want authentic retro BBS experience

### For Development/Testing
- **Use ASCII**: Most reliable for testing ANSI positioning
- **Use UTF-8**: If you need to test Unicode content

## ANSI Art Alignment Issues

The original issue with encoding 407 (EBCDIC) was causing problems because:

1. **EBCDIC is not standard for telnet**: Modern telnet clients expect ASCII-compatible encodings
2. **Multi-byte encodings break ANSI positioning**: ANSI escape sequences assume single-byte characters for cursor positioning
3. **Character width assumptions**: ANSI art relies on each character being exactly one column wide

## Technical Details

The encoding system:
- Automatically registers the CodePages encoding provider for CP437 support
- Falls back to ASCII if the requested encoding is unavailable
- Uses the same encoding for both sending and receiving data
- Maintains proper ANSI escape sequence handling

## Testing

To test different encodings:

```bash
# Test with ASCII (default)
dotnet run -- --console

# Test with CP437 (modify blackboard.yml first)
# Set telnetEncoding: "CP437"
dotnet run -- --console

# Connect with telnet client
telnet localhost 2323
```

## Troubleshooting

If you experience:
- **Misaligned ANSI art**: Switch to ASCII or CP437
- **Missing characters**: Check if your terminal supports the encoding
- **Garbled text**: Ensure client and server encodings match
