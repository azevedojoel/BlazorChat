// Function to scroll to the bottom of the chat container
function scrollToBottom(elementId) {
    var element = document.getElementById(elementId);
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}

// Auto-resize textarea function
window.autoResizeTextArea = function (textAreaElement) {
    if (!textAreaElement) return;
    
    // Reset height to auto to get the correct scrollHeight
    textAreaElement.style.height = 'auto';
    
    // Set the height to match content (with a small buffer)
    const newHeight = Math.min(textAreaElement.scrollHeight, 200); // Max height of 200px
    textAreaElement.style.height = newHeight + 'px';
};

// Initialize chat UI functionality
document.addEventListener('DOMContentLoaded', function() {
    // Auto-focus the input field
    const chatInput = document.getElementById('chatInput');
    if (chatInput) {
        chatInput.focus();
        
        // Setup autosize for the textarea
        chatInput.addEventListener('input', function() {
            window.autoResizeTextArea(chatInput);
        });
    }
    
    // Scroll to bottom on initial load
    scrollToBottom('messageContainer');

    // Add an input event listener to auto-resize as user types
    document.body.addEventListener('input', function(e) {
        if (e.target.tagName.toLowerCase() === 'textarea' && e.target.classList.contains('input-field')) {
            window.autoResizeTextArea(e.target);
        }
    });
}); 