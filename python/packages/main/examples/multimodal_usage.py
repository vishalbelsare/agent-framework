#!/usr/bin/env python3
"""Example: Using multimodal inputs (text + images) with Agent Framework."""

import asyncio
import base64
from agent_framework import ChatAgent, ChatMessage, DataContent, TextContent, UriContent
from agent_framework.openai import OpenAIChatClient


async def main():
    """Demonstrate multimodal agent interactions with text and images."""
    
    # Note: This example requires an OpenAI API key and will make real API calls
    # Make sure you have OPENAI_API_KEY set in your environment
    
    # Create a chat client
    chat_client = OpenAIChatClient()
    
    # Create an agent that can handle multimodal inputs
    agent = ChatAgent(
        chat_client=chat_client,
        instructions="""You are a helpful assistant that can analyze images and text.
        When provided with images, describe what you see in detail.
        Always be helpful and informative in your responses."""
    )
    
    print("🤖 Multimodal Agent Framework Example")
    print("=====================================")
    
    # Example 1: Using a data URI for an embedded image
    print("\n📸 Example 1: Using embedded image data")
    
    # Create a simple 1x1 red pixel as an example image
    # In practice, you would load real image data
    red_pixel_data = b'\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x02\x00\x00\x00\x90wS\xde\x00\x00\x00\x0cIDATx\x9cc\xf8\x0f\x00\x00\x01\x00\x01\x00\x18\xdd\x8d\xb4\x00\x00\x00\x00IEND\xaeB`\x82'
    image_b64 = base64.b64encode(red_pixel_data).decode('utf-8')
    image_data_uri = f"data:image/png;base64,{image_b64}"
    
    # Create multimodal content
    text_content = TextContent("Please analyze this image and tell me what you see:")
    image_content = DataContent(uri=image_data_uri, media_type="image/png")
    
    # Create a multimodal message
    multimodal_message = ChatMessage(
        role="user",
        contents=[text_content, image_content]
    )
    
    try:
        # Send the multimodal message to the agent
        print("Sending multimodal message with embedded image...")
        response = await agent.run(messages=[multimodal_message])
        print(f"Agent Response: {response.text}")
    except Exception as e:
        print(f"Example 1 requires OpenAI API key. Error: {e}")
    
    # Example 2: Using a URI reference to an image
    print("\n🌐 Example 2: Using image URI reference")
    
    # Create content with an image URL
    text_content2 = TextContent("What can you tell me about this image?")
    image_uri_content = UriContent(
        uri="https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/PNG_transparency_demonstration_1.png/280px-PNG_transparency_demonstration_1.png",
        media_type="image/png"
    )
    
    # Create another multimodal message
    multimodal_message2 = ChatMessage(
        role="user", 
        contents=[text_content2, image_uri_content]
    )
    
    try:
        print("Sending multimodal message with image URL...")
        response2 = await agent.run(messages=[multimodal_message2])
        print(f"Agent Response: {response2.text}")
    except Exception as e:
        print(f"Example 2 requires OpenAI API key and internet access. Error: {e}")
    
    # Example 3: Direct client usage (no agent)
    print("\n⚡ Example 3: Direct client usage with multimodal content")
    
    try:
        # Use the chat client directly
        messages = [
            ChatMessage(role="system", text="You are a helpful image analysis assistant."),
            multimodal_message  # Reuse the multimodal message from example 1
        ]
        
        print("Sending multimodal messages directly to chat client...")
        direct_response = await chat_client.get_response(messages=messages)
        print(f"Direct Response: {direct_response.text}")
    except Exception as e:
        print(f"Example 3 requires OpenAI API key. Error: {e}")
    
    print("\n✅ Multimodal examples completed!")
    print("\nKey points:")
    print("- DataContent: Use for embedded image data (data URIs)")
    print("- UriContent: Use for image URLs or file references")  
    print("- Both are automatically converted to OpenAI's image_url format")
    print("- Works with both ChatAgent and direct client usage")
    print("- Supports all common image formats (PNG, JPEG, GIF, WebP, etc.)")


def create_sample_multimodal_message():
    """Helper function to create a sample multimodal message without API calls."""
    print("\n🔧 Creating sample multimodal message structure:")
    
    # Create sample content
    text = TextContent("Analyze this image:")
    
    # Sample data URI (1x1 red pixel)
    sample_data = b"fake_image_data_here"
    sample_b64 = base64.b64encode(sample_data).decode('utf-8')
    data_uri = f"data:image/png;base64,{sample_b64}"
    image_data = DataContent(uri=data_uri, media_type="image/png")
    
    # Sample image URL
    image_url = UriContent(
        uri="https://example.com/image.jpg",
        media_type="image/jpeg"
    )
    
    # Create multimodal message
    message = ChatMessage(
        role="user",
        contents=[text, image_data, image_url]
    )
    
    print(f"✓ Message created with {len(message.contents)} content items:")
    for i, content in enumerate(message.contents):
        print(f"  {i+1}. {type(content).__name__}: {content.type}")
        if hasattr(content, 'text'):
            print(f"     Text: {content.text}")
        elif hasattr(content, 'uri'):
            print(f"     URI: {content.uri[:50]}...")
            print(f"     Media Type: {content.media_type}")
    
    return message


if __name__ == "__main__":
    # First show how to create multimodal messages
    sample_message = create_sample_multimodal_message()
    
    print("\n" + "="*60)
    print("To run the full examples with OpenAI API:")
    print("1. Set your OPENAI_API_KEY environment variable")
    print("2. Uncomment the line below and run again")
    print("="*60)
    
    # Uncomment the next line to run the full examples (requires OpenAI API key)
    # asyncio.run(main())