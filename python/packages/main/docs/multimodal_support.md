# Multimodal Input Support

The Agent Framework supports multimodal inputs, allowing you to combine text and images in your conversations with AI agents. This feature enables more sophisticated interactions where agents can analyze images, diagrams, charts, and other visual content alongside text.

## Overview

Multimodal support in the Agent Framework is implemented through two main content types:

- **DataContent**: For embedded image data using data URIs (base64-encoded images)
- **UriContent**: For image URLs and file references

Both content types are automatically converted to the appropriate format required by the underlying AI service (e.g., OpenAI's `image_url` format).

## Supported Image Formats

The framework supports all common image formats:

- PNG (`image/png`)
- JPEG (`image/jpeg`) 
- GIF (`image/gif`)
- WebP (`image/webp`)
- SVG (`image/svg+xml`)
- BMP (`image/bmp`)
- TIFF (`image/tiff`)
- APNG (`image/apng`)
- AVIF (`image/avif`)

## Usage Examples

### Basic Multimodal Message

```python
import base64
from agent_framework import ChatMessage, DataContent, TextContent, UriContent

# Create text content
text = TextContent("Please analyze this image:")

# Create image content from a data URI
with open("image.png", "rb") as f:
    image_data = f.read()
    image_b64 = base64.b64encode(image_data).decode('utf-8')
    image_uri = f"data:image/png;base64,{image_b64}"

image_content = DataContent(uri=image_uri, media_type="image/png")

# Create a multimodal message
message = ChatMessage(
    role="user",
    contents=[text, image_content]
)
```

### Using Image URLs

```python
from agent_framework import ChatMessage, TextContent, UriContent

# Create content with an image URL
text = TextContent("What can you see in this image?")
image_url = UriContent(
    uri="https://example.com/image.jpg",
    media_type="image/jpeg"
)

message = ChatMessage(
    role="user",
    contents=[text, image_url]
)
```

### With ChatAgent

```python
import asyncio
from agent_framework import ChatAgent
from agent_framework.openai import OpenAIChatClient

async def main():
    # Create an agent
    agent = ChatAgent(
        chat_client=OpenAIChatClient(),
        instructions="You are a helpful assistant that can analyze images."
    )
    
    # Send multimodal message
    response = await agent.run(messages=[message])
    print(response.text)

asyncio.run(main())
```

### Direct Client Usage

```python
import asyncio
from agent_framework import ChatMessage
from agent_framework.openai import OpenAIChatClient

async def main():
    client = OpenAIChatClient()
    
    messages = [
        ChatMessage(role="system", text="You are an image analysis assistant."),
        message  # Your multimodal message
    ]
    
    response = await client.get_response(messages=messages)
    print(response.text)

asyncio.run(main())
```

## Implementation Details

### Automatic Format Conversion

When you use `DataContent` or `UriContent` with image media types, the framework automatically converts them to the format expected by the AI service:

```python
# Your DataContent/UriContent
{
    "type": "data",  # or "uri"
    "uri": "data:image/png;base64,....." ,
    "media_type": "image/png"
}

# Automatically converted to OpenAI format
{
    "type": "image_url",
    "image_url": {"url": "data:image/png;base64,....."}
}
```

### Non-Image Content

Non-image content types (e.g., PDFs, text files) are not converted and will use the default format:

```python
# PDF content remains unchanged
pdf_content = DataContent(
    uri="data:application/pdf;base64,.....",
    media_type="application/pdf"
)
# Results in: {"type": "data", "uri": "...", "media_type": "application/pdf"}
```

### Supported Clients

Multimodal support is available in:

- `OpenAIChatClient` - For chat completions
- `OpenAIResponsesClient` - For structured responses

## Error Handling

The framework validates media types and URI formats:

```python
# Valid - supported image format
DataContent(uri="data:image/png;base64,.....", media_type="image/png")

# Invalid - unsupported media type
DataContent(uri="data:image/xyz;base64,.....", media_type="image/xyz")  # ValidationError

# Invalid - malformed URI
DataContent(uri="invalid-uri", media_type="image/png")  # ValidationError
```

## Performance Considerations

- **Data URIs**: Include the entire image data in the request, which can make requests larger
- **Image URLs**: More efficient for large images but require the URL to be accessible
- **Image Size**: Consider resizing large images before encoding to reduce request size

## Best Practices

1. **Choose the right content type**:
   - Use `DataContent` for small images or when you need to embed the image directly
   - Use `UriContent` for larger images or when the image is already hosted online

2. **Optimize image size**:
   - Resize images to appropriate dimensions before sending
   - Use appropriate compression settings

3. **Validate inputs**:
   - Ensure image URLs are accessible
   - Validate base64-encoded data before creating DataContent

4. **Handle errors gracefully**:
   - Catch validation errors for malformed URIs or unsupported formats
   - Provide fallback behavior for inaccessible image URLs

## Complete Example

See the [multimodal usage example](../examples/multimodal_usage.py) for a complete working demonstration of multimodal capabilities.