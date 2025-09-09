# Copyright (c) Microsoft. All rights reserved.

import base64
import pytest

from agent_framework import ChatMessage, DataContent, Role, TextContent, UriContent
from agent_framework.openai import OpenAIChatClient, OpenAIResponsesClient


class TestMultimodalContent:
    """Test multimodal content support in OpenAI clients."""

    def test_chat_client_image_data_content_parsing(self):
        """Test that DataContent with image media type is converted to image_url format."""
        client = OpenAIChatClient(api_key="test-key", ai_model_id="gpt-4")
        
        sample_image_data = b"fake_image_data"
        sample_image_b64 = base64.b64encode(sample_image_data).decode("utf-8")
        sample_image_uri = f"data:image/png;base64,{sample_image_b64}"
        
        image_data_content = DataContent(uri=sample_image_uri, media_type="image/png")
        parsed_data = client._openai_content_parser(image_data_content)
        
        expected_data = {
            "type": "image_url",
            "image_url": {"url": sample_image_uri}
        }
        assert parsed_data == expected_data

    def test_chat_client_image_uri_content_parsing(self):
        """Test that UriContent with image media type is converted to image_url format."""
        client = OpenAIChatClient(api_key="test-key", ai_model_id="gpt-4")
        
        image_uri_content = UriContent(uri="https://example.com/image.jpg", media_type="image/jpeg")
        parsed_uri = client._openai_content_parser(image_uri_content)
        
        expected_uri = {
            "type": "image_url",
            "image_url": {"url": "https://example.com/image.jpg"}
        }
        assert parsed_uri == expected_uri

    def test_chat_client_non_image_data_content_fallback(self):
        """Test that non-image DataContent falls back to model_dump format."""
        client = OpenAIChatClient(api_key="test-key", ai_model_id="gpt-4")
        
        pdf_data_content = DataContent(uri="data:application/pdf;base64,test", media_type="application/pdf")
        parsed_pdf = client._openai_content_parser(pdf_data_content)
        
        expected_pdf = {
            "type": "data",
            "uri": "data:application/pdf;base64,test",
            "media_type": "application/pdf"
        }
        assert parsed_pdf == expected_pdf

    def test_chat_client_non_image_uri_content_fallback(self):
        """Test that non-image UriContent falls back to model_dump format."""
        client = OpenAIChatClient(api_key="test-key", ai_model_id="gpt-4")
        
        text_uri_content = UriContent(uri="https://example.com/document.txt", media_type="text/plain")
        parsed_text_uri = client._openai_content_parser(text_uri_content)
        
        expected_text_uri = {
            "type": "uri",
            "uri": "https://example.com/document.txt",
            "media_type": "text/plain"
        }
        assert parsed_text_uri == expected_text_uri

    def test_responses_client_image_data_content_parsing(self):
        """Test that DataContent with image media type is converted to image_url format in responses client."""
        client = OpenAIResponsesClient(api_key="test-key", ai_model_id="gpt-4")
        
        sample_image_data = b"fake_image_data"
        sample_image_b64 = base64.b64encode(sample_image_data).decode("utf-8")
        sample_image_uri = f"data:image/png;base64,{sample_image_b64}"
        
        image_data_content = DataContent(uri=sample_image_uri, media_type="image/png")
        parsed_data = client._openai_content_parser(Role.USER, image_data_content, {})
        
        expected_data = {
            "type": "image_url",
            "image_url": {"url": sample_image_uri}
        }
        assert parsed_data == expected_data

    def test_responses_client_image_uri_content_parsing(self):
        """Test that UriContent with image media type is converted to image_url format in responses client."""
        client = OpenAIResponsesClient(api_key="test-key", ai_model_id="gpt-4")
        
        image_uri_content = UriContent(uri="https://example.com/image.jpg", media_type="image/jpeg")
        parsed_uri = client._openai_content_parser(Role.USER, image_uri_content, {})
        
        expected_uri = {
            "type": "image_url",
            "image_url": {"url": "https://example.com/image.jpg"}
        }
        assert parsed_uri == expected_uri

    def test_multimodal_message_creation(self):
        """Test creating a multimodal message with text and images."""
        text_content = TextContent("Please describe this image:")
        
        image_data = b"fake_image_data"
        image_b64 = base64.b64encode(image_data).decode("utf-8")
        image_uri = f"data:image/png;base64,{image_b64}"
        image_content = DataContent(uri=image_uri, media_type="image/png")
        
        web_image_content = UriContent(uri="https://example.com/photo.jpg", media_type="image/jpeg")
        
        message = ChatMessage(
            role="user",
            contents=[text_content, image_content, web_image_content]
        )
        
        assert len(message.contents) == 3
        assert isinstance(message.contents[0], TextContent)
        assert isinstance(message.contents[1], DataContent)
        assert isinstance(message.contents[2], UriContent)
        
        # Test that the text property works with multimodal content
        assert message.text == "Please describe this image:"

    @pytest.mark.parametrize("media_type,uri", [
        ("image/png", "data:image/png;base64,test"),
        ("image/jpeg", "data:image/jpeg;base64,test"),
        ("image/gif", "data:image/gif;base64,test"),
        ("image/webp", "data:image/webp;base64,test"),
        ("image/svg+xml", "data:image/svg+xml;base64,test"),
        ("image/bmp", "data:image/bmp;base64,test"),
        ("image/tiff", "data:image/tiff;base64,test"),
        ("image/apng", "data:image/apng;base64,test"),
        ("image/avif", "data:image/avif;base64,test"),
    ])
    def test_different_image_formats(self, media_type, uri):
        """Test that different image formats are handled correctly."""
        client = OpenAIChatClient(api_key="test-key", ai_model_id="gpt-4")
        
        content = DataContent(uri=uri, media_type=media_type)
        parsed = client._openai_content_parser(content)
        
        expected = {
            "type": "image_url",
            "image_url": {"url": uri}
        }
        assert parsed == expected

    def test_image_content_has_top_level_media_type(self):
        """Test that the has_top_level_media_type method works correctly for images."""
        # Test DataContent
        image_data_content = DataContent(
            uri="data:image/png;base64,test", 
            media_type="image/png"
        )
        assert image_data_content.has_top_level_media_type("image") is True
        assert image_data_content.has_top_level_media_type("text") is False
        
        # Test UriContent  
        image_uri_content = UriContent(
            uri="https://example.com/image.jpg",
            media_type="image/jpeg"
        )
        assert image_uri_content.has_top_level_media_type("image") is True
        assert image_uri_content.has_top_level_media_type("application") is False
        
        # Test non-image content
        pdf_content = DataContent(
            uri="data:application/pdf;base64,test",
            media_type="application/pdf"  
        )
        assert pdf_content.has_top_level_media_type("application") is True
        assert pdf_content.has_top_level_media_type("image") is False