# 使用 BootStrap 4.0

``` html
<link rel="stylesheet" href="./css/bootstrap.css" crossorigin="anonymous">
<script src="./js/bootstrap.js" crossorigin="anonymous"></script>

<body>
    <div class="container" style="padding: 20px;">
        <div class="row">&nbsp;</div>
        <div class="row">
            <div class="col-2">User</div>
            <div class="col-4"><input type="text" id="userInput" value="Bob" /></div>
        </div>
        <div class="row">
            <div class="col-2">Message</div>
            <div class="col-4"><input type="text" id="messageInput" value="Hi" /></div>
        </div>
        <div class="row">&nbsp;</div>
        <div class="row">
            <div class="col-6">
                <button  id="sendButton">Send Message</button>
            </div>
        </div>
    </div>
</body>

```