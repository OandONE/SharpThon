from fast_rub import Client
from fast_rub.types import Message

bot = Client("test")

bot.start()

@bot.on_message()
async def main(msg: Message){
    print(msg)
}

bot.run()
