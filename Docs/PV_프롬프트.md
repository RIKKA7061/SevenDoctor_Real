# Rewind Manor — 실사 시네마틱 예고편 (Kling AI 프롬프트)

목표: 유튜브 바이럴. **쇼츠(9:16, 약 35초)가 주력**이고, 같은 컷으로 **본편 예고편(16:9, 약 75초)**도 만듭니다.

---

## 먼저 알아 둘 세 가지

1. **실사와 게임 그림이 다르다는 걸 숨기지 마세요.**
   제목이나 영상에 `Cinematic Trailer`라고 적고, 마지막 3~5초는 실제 게임 화면으로 끝내세요.
   실사만 보고 게임을 받은 사람이 "속았다"고 느끼면 댓글이 그 얘기로 덮이고, 그러면 바이럴이 역효과가 납니다.
   반대로 "영화 같은 예고편 → 실제 게임은 이런 손그림"으로 이어지면 오히려 화제가 되기 좋습니다.
2. **유튜브에 올릴 때 '변경되거나 합성된 콘텐츠' 표시를 켜야 합니다.**
   실사처럼 보이는 AI 인물이 나오면 공개 의무가 있습니다. 업로드 화면 '세부정보'에 있습니다.
3. **글자, 자막, 로고는 Kling에 시키지 마세요.**
   AI는 글자를 망가뜨립니다. 프롬프트에는 "no text"를 넣고, 글자는 편집 프로그램(CapCut, Premiere 등)에서 올리세요.

---

## 이 이야기가 실사 예고편에 유리한 이유

**등장인물 8명 중 7명이 같은 얼굴입니다**(평행세계의 박사들). AI 영상의 가장 큰 약점이 컷마다 얼굴이 바뀌는 건데, 이 이야기는 **한 얼굴만 고정하면 끝**입니다.
"같은 얼굴 7명이 한 방에 서 있는 장면"은 그 자체로 소름 돋는 썸네일이 됩니다.

게임과 연결되도록 상징물은 그대로 가져갑니다.
- 박사: 정수리에 묶은 갈색 머리, 머리 위 황동 고글, 머리에 꽂은 연필, 흰 가운
- 교만(딸): 동그란 안경, 기운 자국이 있는 회색 코트
- 시계: **3시 14분에 멈춤**
- 촛불: **6개**

---

## 0단계 — 배우 레퍼런스 이미지 먼저 만들기 (가장 중요)

영상보다 **이미지를 먼저** 만듭니다. Kling의 이미지 생성(또는 Midjourney 등)으로 얼굴 사진 한 장을 확정하고, 그 사진을 **모든 영상의 레퍼런스(Elements / 참조 이미지)**로 넣습니다.

**박사 얼굴 (기준 사진)**
```
Cinematic portrait photo of a woman in her early 40s, tired intelligent eyes, light freckles,
messy chestnut-brown hair tied in a loose bun with a yellow pencil stuck through it,
old brass welding goggles pushed up on her head, white lab coat with rolled sleeves over a coral sweater,
neutral expression, looking straight at camera, soft key light from the left, dark brick background,
shot on 85mm lens, shallow depth of field, film grain, realistic skin texture
```

이 사진을 넣고 **의상만 바꿔서** 평행세계의 박사 6명을 만듭니다. 얼굴은 같게 유지합니다.

| 인물 | 레퍼런스에 덧붙일 문장 |
|---|---|
| 분노 Wrath | `same woman, sharp short red hair, scorched leather apron, soot on cheeks, clenched jaw` |
| 슬픔 Sorrow | `same woman, long damp hair, oversized grey cardigan, red swollen eyes, holding a burnt candle` |
| 오만 Arrogance | `same woman, slicked-back purple ponytail, tailored charcoal three-piece suit, chin raised, cold stare` |
| 탐욕 Greed | `same woman, long golden hair, ornate pale-green Victorian gown, gold rings, holding a coin pouch` |
| 색욕 Lust | `same woman, voluminous wine-red curls, round glasses, burgundy corset dress, half-smile` |
| 나태 Sloth | `same woman, messy sage-green bob, round glasses, patched baggy coat, half-closed sleepy eyes` |

**교만 Pride (딸)** — 다른 얼굴이지만 박사를 닮게 만듭니다.
```
Cinematic portrait of a 16-year-old girl who resembles the woman in the reference, dark purple bob haircut,
large round glasses, patched grey wool coat, quiet unreadable expression, candlelight from below,
85mm, shallow depth of field, film grain
```
회상용 10살 버전도 같은 방식으로 한 장 만드세요: `same girl at age 10, party hat, birthday dress`.

**레퍼런스 7장을 확정하기 전에는 영상을 돌리지 마세요.** 얼굴이 흔들리면 뒤에 만드는 모든 영상이 흔들립니다.

---

## 모든 프롬프트 뒤에 붙일 공통 문장

**스타일 (프롬프트 맨 끝에 붙이기)**
```
cinematic film still, anamorphic 2.39 look, moody low-key lighting, warm tungsten practicals with cold teal moonlight,
volumetric dust in light beams, shallow depth of field, 35mm film grain, Victorian gothic mansion,
dark fantasy mystery thriller, color graded like a prestige movie trailer
```

**네거티브 프롬프트**
```
cartoon, anime, 3D render, CGI look, plastic skin, text, subtitles, watermark, logo,
extra fingers, deformed hands, face morphing, identity change, extra people, blurry, flicker, jitter, oversaturated
```

**Kling 설정 요령**
- **이미지→영상**으로 만드세요. 첫 프레임을 이미지로 고정하면 얼굴과 구도가 안 흔들립니다. 텍스트→영상은 배경이나 효과 컷에만 쓰세요.
- 한 컷에는 **동작 하나와 카메라 움직임 하나**만 넣으세요. 여러 개를 넣으면 망가집니다.
- 길이는 5초를 기본으로 하고, 같은 프롬프트로 3~4번 뽑아서 제일 좋은 걸 고르세요.
- 변신 컷(평온 → 섬광 같은)은 **시작 프레임과 끝 프레임**을 둘 다 넣으세요.
- 쇼츠용으로는 피사체를 **화면 가운데**에 두세요(`subject centered`). 그래야 16:9를 9:16으로 잘라도 살아남습니다.

---

## 쇼츠 콘티 (9:16, 약 35초) — 주력

음악은 **째깍째깍 시계 소리**로 시작해서 점점 빨라지다가, 3:14에서 **완전히 멈추는 무음**으로 설계하세요. 바이럴 쇼츠는 소리로 끝까지 붙잡습니다.

### 첫 2초 — 엄지를 멈추게 하는 컷

| # | 길이 | 화면 글자 (편집에서) |
|---|---|---|
| 1 | 0–2초 | **"평행세계의 내가 7명 있다."** |

```
Extreme close-up of the woman's face (reference), she slowly opens her eyes in the dark,
and six identical faces appear one by one behind her shoulder out of the shadows, all staring at camera,
slow push-in, subject centered
```

### 사건 (2–10초)

**#2 (2–4초)** 레버를 당기는 손. 화면 글자: **"20년 만에 완성한 타임머신."**
```
Close-up of a woman's hand in a lab coat sleeve gripping a heavy brass lever on a riveted copper time machine,
a large clock dial on the machine, a blue crystal begins to glow, she pulls the lever down hard,
sparks burst, camera shakes slightly
```

**#3 (4–6초)** 하얀 섬광. 시작 프레임은 #2의 마지막 장면, 끝 프레임은 하얀 화면으로 넣습니다.
```
The copper time machine screams and overloads, blinding white light tears through the underground brick lab,
papers and gears fly outward, the woman shields her face, fast push-in into the white
```

**#4 (6–10초)** 저택과 3시 14분. 화면 글자: **"새벽 3시 14분. 시간이 멈춘 저택."**
```
Wide shot, a towering abandoned Victorian mansion on a hill at night, fog rolling across the dead garden,
a clock tower with hands frozen at 3:14, a few windows glowing amber, full moon behind clouds,
the woman in a lab coat stands small in the foreground looking up, slow crane up
```

### 미스터리 (10–22초)

**#5 (10–13초)** 같은 얼굴. 화면 글자: **"…내 얼굴이잖아."**
```
Dark mansion doorway, a sleepy woman with a green bob and round glasses (same face as reference)
steps slowly out of the shadow into moonlight, she tilts her head and smiles faintly,
over-the-shoulder shot from the doctor's point of view
```

**#6 (13–17초)** 일곱 명이 한 줄로 선 장면. 썸네일 후보입니다.
```
Grand candlelit mansion lobby, seven women with the exact same face but different outfits
(lab coat, leather apron, grey cardigan, charcoal suit, Victorian gown, burgundy corset, patched coat)
stand in a line facing camera, slow dolly sideways across their faces, tense silence
```
같은 얼굴 7명을 한 번에 그리는 건 어렵습니다. 잘 안 나오면 **인물별로 1초짜리 컷 7개**를 따로 만들어 빠르게 이어 붙이세요. 그게 더 긴장감이 납니다.

**#7 (17–20초)** 부서진 타임머신. 화면 글자: **"누군가 매번, 타임머신을 부순다."**
```
A smashed time machine drive assembly in a dark workshop, broken gears and sparking wires,
a cracked clock face stopped at 3:12, smoke curling up, slow orbit around the wreckage
```

**#8 (20–22초)** 서로 의심하는 눈빛. 아주 빠른 컷 3개를 이어 붙입니다.
```
Extreme close-up of eyes: the woman in the suit glancing sideways, the woman in the gown narrowing her eyes,
the woman with red curls smirking — quick rack focus
```

### 감정 (22–31초) — 바이럴은 여기서 터집니다

**#9 (22–25초)** 찢어진 쪽지. 쪽지 글씨는 편집에서 넣습니다: **"엄마, 6시까지 와 줘."**
```
Torn pieces of a child's handwritten note on a dusty floor slowly slide together under candlelight,
the woman's trembling fingers, macro shot, shallow depth of field
```

**#10 (25–28초)** 다 타 버린 생일 초 6개.
```
A small birthday cake on a table in a dark empty room, six candles burned all the way down,
one thin thread of smoke rising, a party hat lying on the floor, a clock on the wall reads 3:14,
very slow push-in, silence
```

**#11 (28–31초)** 범인의 실루엣. 얼굴을 다 보여 주지 말고, 화면 글자는 **"범인은 평행세계의 내가 아니었다."**
```
A teenage girl with a dark bob and large round glasses stands with her back to camera in a candlelit basement,
she slowly turns her head, candle flames reflected in her glasses hide her eyes, slow push-in
```

### 끝 (31–35초)

**#12 (31–33초)** 일곱 번째 촛불. 화면 글자: **"이번엔, 일곱 개."**
```
In darkness, a woman's hand lights candles on a birthday cake one by one with a match, the seventh flame catches,
warm light slowly fills the frame and reveals a girl's face softening, close-up
```

**#13 (33–35초)** 제목 카드와 **실제 게임 화면**(연구실 손그림 + 로봇)입니다. Kling 없이 편집에서 만듭니다.
- 글자: `Rewind Manor` / `위시리스트 추가` / `Cinematic trailer · 실제 게임은 손그림 어드벤처`

---

## 본편 예고편 (16:9, 약 75초)

쇼츠 컷을 그대로 쓰고, 이 컷들을 추가합니다.

| 위치 | 추가 컷 | 프롬프트 요지 |
|---|---|---|
| #2 앞 | 연구 노트 | `research notebook, last page, a daughter's name crossed out in ink, the woman's hand hesitates` |
| #5 뒤 | 평면도 | `a teenage girl in round glasses hands over a worn floor plan of the mansion in a dark lobby, the paper soft from years of handling` |
| #8 뒤 | 퍼즐 3종 | 실제 게임 화면을 녹화하세요(OBS). 금고 타일, 퀸 체스판, 슬라이딩. 실사 컷 사이에 1초씩 끼우면 "이게 게임이구나"가 전달됩니다 |
| #10 뒤 | 시상식 회상 | `flashback, the woman receives a science award on a bright stage, camera flashes, applause — intercut with a little girl alone at home watching it on TV` |
| #12 뒤 | 세 갈래 선택 | 검은 화면에 글자 세 줄: 함께 고친다 / 남는다 / 혼자 떠난다 |

회상 컷은 색을 다르게 하세요. 프롬프트에 `overexposed warm flashback, soft halation`을 붙이면 현재 장면과 구분됩니다.

---

## 업로드할 때

- **제목 후보:** `평행세계의 내가 7명… 그중 한 명이 범인이다` / `7 versions of me are trapped in this mansion. One of us is lying.`
- **썸네일:** #6처럼 같은 얼굴 7명이 한 줄로 선 장면에, 안경에 촛불이 비친 소녀를 한쪽에 작게 넣으세요. 글자는 3단어 이내로.
- **고정 댓글:** 스팀 위시리스트 링크와 "실제 게임은 이런 그림입니다" 스크린샷 한 장.
- **한영 자막 두 벌.** 화면 글자도 영어판을 따로 뽑아 두면 해외 쇼츠 피드에도 실립니다.
- **AI 합성 표시를 켜세요**(위 2번).
