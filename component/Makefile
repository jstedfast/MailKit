all:

#build:
#	xbuild /property:Configuration=Release /target:Rebuild ../MailKit.Mobile.sln

package: ./tools/
	mono ./tools/xamarin-component.exe package

validate: ./tools/
	mono ./tools/xamarin-component.exe validate

upload: ./tools/
	mono ./tools/xamarin-component.exe upload

./tools/:
	rm -rf ./tools/
	mkdir ./tools/
	cd ./tools/ && curl -silent -L https://components.xamarin.com/submit/xpkg > tools.zip
	-cd ./tools/ && unzip tools.zip
	rm ./tools/tools.zip

clean:
	rm -rf ./tools/
